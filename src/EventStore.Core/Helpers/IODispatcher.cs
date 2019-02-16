using System;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Core.Services.TimerService;
using System.Collections.Generic;

namespace EventStore.Core.Helpers {
	public sealed class IODispatcher : IHandle<IODispatcherDelayedMessage> {
		public const int ReadTimeoutMs = 10000;

		private readonly Guid _selfId = Guid.NewGuid();
		private readonly IPublisher _publisher;
		private readonly IEnvelope _inputQueueEnvelope;
		private readonly WriterQueueSet _writerQueueSet = new WriterQueueSet();
		private readonly PendingRequests _pendingRequests = new PendingRequests();
		private readonly PendingReads _pendingReads = new PendingReads();

		public readonly
			RequestResponseDispatcher
			<ClientMessage.ReadStreamEventsForward, ClientMessage.ReadStreamEventsForwardCompleted> ForwardReader;

		public readonly
			RequestResponseDispatcher
			<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> BackwardReader;

		public readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> Writer;

		public readonly RequestResponseDispatcher<ClientMessage.DeleteStream, ClientMessage.DeleteStreamCompleted>
			StreamDeleter;

		public readonly RequestResponseDispatcher<AwakeServiceMessage.SubscribeAwake, IODispatcherDelayedMessage>
			Awaker;

		public IODispatcher(IPublisher publisher, IEnvelope envelope) {
			_publisher = publisher;
			_inputQueueEnvelope = envelope;
			ForwardReader =
				new RequestResponseDispatcher
					<ClientMessage.ReadStreamEventsForward, ClientMessage.ReadStreamEventsForwardCompleted>(
						publisher,
						v => v.CorrelationId,
						v => v.CorrelationId,
						envelope);
			BackwardReader =
				new RequestResponseDispatcher
					<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>(
						publisher,
						v => v.CorrelationId,
						v => v.CorrelationId,
						envelope);
			Writer =
				new RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>(
					publisher,
					v => v.CorrelationId,
					v => v.CorrelationId,
					envelope);

			StreamDeleter =
				new RequestResponseDispatcher<ClientMessage.DeleteStream, ClientMessage.DeleteStreamCompleted>(
					publisher,
					v => v.CorrelationId,
					v => v.CorrelationId,
					envelope);

			Awaker =
				new RequestResponseDispatcher<AwakeServiceMessage.SubscribeAwake, IODispatcherDelayedMessage>(
					publisher,
					v => v.CorrelationId,
					v => v.CorrelationId,
					envelope,
					cancelMessageFactory: requestId => new AwakeServiceMessage.UnsubscribeAwake(requestId));
		}

		public Guid ReadBackward(
			string streamId,
			long fromEventNumber,
			int maxCount,
			bool resolveLinks,
			IPrincipal principal,
			Action<ClientMessage.ReadStreamEventsBackwardCompleted> action,
			Guid? corrId = null) {
			if (!corrId.HasValue)
				corrId = Guid.NewGuid();
			return
				BackwardReader.Publish(
					new ClientMessage.ReadStreamEventsBackward(
						corrId.Value,
						corrId.Value,
						BackwardReader.Envelope,
						streamId,
						fromEventNumber,
						maxCount,
						resolveLinks,
						false,
						null,
						principal),
					action);
		}

		public Guid ReadBackward(
			string streamId,
			long fromEventNumber,
			int maxCount,
			bool resolveLinks,
			IPrincipal principal,
			Action<ClientMessage.ReadStreamEventsBackwardCompleted> action,
			Action timeoutAction,
			Guid corrId) {
			_pendingReads.Register(corrId);

			BackwardReader.Publish(
				new ClientMessage.ReadStreamEventsBackward(
					corrId,
					corrId,
					BackwardReader.Envelope,
					streamId,
					fromEventNumber,
					maxCount,
					resolveLinks,
					false,
					null,
					principal),
				res => {
					if (!_pendingReads.IsRegistered(res.CorrelationId)) return;
					_pendingReads.Remove(res.CorrelationId);
					action(res);
				}
			);
			Delay(TimeSpan.FromMilliseconds(ReadTimeoutMs), () => {
				if (!_pendingReads.IsRegistered(corrId)) return;
				_pendingReads.Remove(corrId);
				timeoutAction();
			}, corrId);
			return corrId;
		}

		public Guid ReadForward(
			string streamId,
			long fromEventNumber,
			int maxCount,
			bool resolveLinks,
			IPrincipal principal,
			Action<ClientMessage.ReadStreamEventsForwardCompleted> action,
			Guid? corrId = null) {
			if (!corrId.HasValue)
				corrId = Guid.NewGuid();
			return
				ForwardReader.Publish(
					new ClientMessage.ReadStreamEventsForward(
						corrId.Value,
						corrId.Value,
						ForwardReader.Envelope,
						streamId,
						fromEventNumber,
						maxCount,
						resolveLinks,
						false,
						null,
						principal),
					action);
		}

		public Guid ReadForward(
			string streamId,
			long fromEventNumber,
			int maxCount,
			bool resolveLinks,
			IPrincipal principal,
			Action<ClientMessage.ReadStreamEventsForwardCompleted> action,
			Action timeoutAction,
			Guid corrId) {
			_pendingReads.Register(corrId);

			ForwardReader.Publish(
				new ClientMessage.ReadStreamEventsForward(
					corrId,
					corrId,
					ForwardReader.Envelope,
					streamId,
					fromEventNumber,
					maxCount,
					resolveLinks,
					false,
					null,
					principal),
				res => {
					if (!_pendingReads.IsRegistered(corrId)) return;
					_pendingReads.Remove(corrId);
					action(res);
				});
			Delay(TimeSpan.FromMilliseconds(ReadTimeoutMs), () => {
				if (!_pendingReads.IsRegistered(corrId)) return;
				_pendingReads.Remove(corrId);
				timeoutAction();
			}, corrId);
			return corrId;
		}

		public void ConfigureStreamAndWriteEvents(
			string streamId,
			long expectedVersion,
			Lazy<StreamMetadata> streamMetadata,
			Event[] events,
			IPrincipal principal,
			Action<ClientMessage.WriteEventsCompleted> action) {
			if (expectedVersion != ExpectedVersion.Any && expectedVersion != ExpectedVersion.NoStream)
				WriteEvents(streamId, expectedVersion, events, principal, action);
			else
				ReadBackward(
					streamId,
					-1,
					1,
					false,
					principal,
					completed => {
						switch (completed.Result) {
							case ReadStreamResult.Success:
							case ReadStreamResult.NoStream:
								if (completed.Events != null && completed.Events.Length > 0)
									WriteEvents(streamId, expectedVersion, events, principal, action);
								else
									UpdateStreamAcl(
										streamId,
										ExpectedVersion.Any,
										principal,
										streamMetadata.Value,
										metaCompleted =>
											WriteEvents(streamId, expectedVersion, events, principal, action));
								break;
							case ReadStreamResult.AccessDenied:
								action(
									new ClientMessage.WriteEventsCompleted(
										Guid.NewGuid(),
										OperationResult.AccessDenied,
										""));
								break;
							case ReadStreamResult.StreamDeleted:
								action(
									new ClientMessage.WriteEventsCompleted(
										Guid.NewGuid(),
										OperationResult.StreamDeleted,
										""));
								break;
							default:
								throw new NotSupportedException();
						}
					});
		}

		public Guid WriteEvents(
			string streamId,
			long expectedVersion,
			Event[] events,
			IPrincipal principal,
			Action<ClientMessage.WriteEventsCompleted> action) {
			var corrId = Guid.NewGuid();
			return
				Writer.Publish(
					new ClientMessage.WriteEvents(
						corrId,
						corrId,
						Writer.Envelope,
						false,
						streamId,
						expectedVersion,
						events,
						principal),
					action);
		}

		private class PendingRequests {
			private readonly Dictionary<Guid, Action<ClientMessage.WriteEventsCompleted>> _map;

			public PendingRequests() {
				_map = new Dictionary<Guid, Action<ClientMessage.WriteEventsCompleted>>();
			}

			public void CaptureCallback(Guid correlationId, Action<ClientMessage.WriteEventsCompleted> action) {
				_map.Add(correlationId, action);
			}

			public void CompleteRequest(ClientMessage.WriteEventsCompleted message) {
				Action<ClientMessage.WriteEventsCompleted> action;
				if (_map.TryGetValue(message.CorrelationId, out action)) {
					_map.Remove(message.CorrelationId);
					action(message);
				}
			}
		}

		private class PendingReads {
			private readonly HashSet<Guid> _pendingReads = new HashSet<Guid>();

			public void Register(Guid id) {
				_pendingReads.Add(id);
			}

			public bool IsRegistered(Guid id) {
				var ret = _pendingReads.Contains(id);
				return ret;
			}

			public void Remove(Guid id) {
				_pendingReads.Remove(id);
			}
		}

		private class WriterQueueSet {
			private readonly Dictionary<Guid, WriterQueue> _queues;

			public WriterQueueSet() {
				_queues = new Dictionary<Guid, WriterQueue>();
			}

			public void AddToQueue(Guid key, ClientMessage.WriteEvents message) {
				WriterQueue writerQueue;
				if (!_queues.TryGetValue(key, out writerQueue)) {
					writerQueue = new WriterQueue();
					_queues.Add(key, writerQueue);
				}

				writerQueue.Enqueue(message);
			}

			public void Finish(Guid key) {
				var queue = GetQueue(key);
				if (queue == null) return;
				queue.IsBusy = false;

				CleanupQueue(key, queue);
			}

			public bool IsBusy(Guid key) =>
				GetQueue(key)?.IsBusy ?? false;

			public bool HasPendingWrites(Guid key) =>
				GetQueue(key)?.Count > 0;

			public ClientMessage.WriteEvents Dequeue(Guid key) =>
				GetQueue(key)?.Dequeue();

			private WriterQueue GetQueue(Guid key) {
				WriterQueue queue;
				_queues.TryGetValue(key, out queue);
				return queue;
			}

			private void CleanupQueue(Guid key, WriterQueue queue) {
				if (queue.IsBusy) return;
				if (queue.Count > 0) return;
				_queues.Remove(key);
			}
		}

		private class WriterQueue {
			private readonly Queue<ClientMessage.WriteEvents> _queue;
			public bool IsBusy;
			public int Count => _queue.Count;

			public WriterQueue() {
				IsBusy = false;
				_queue = new Queue<ClientMessage.WriteEvents>();
			}

			public void Enqueue(ClientMessage.WriteEvents message) {
				_queue.Enqueue(message);
			}

			public ClientMessage.WriteEvents Dequeue() {
				if (_queue.Count == 0) return null;

				IsBusy = true;

				return _queue.Dequeue();
			}
		}

		public Guid QueueWriteEvents(
			Guid key,
			string streamId,
			long expectedVersion,
			Event[] events,
			IPrincipal principal,
			Action<ClientMessage.WriteEventsCompleted> action) {
			var corrId = Guid.NewGuid();
			var message = new ClientMessage.WriteEvents(
				corrId,
				corrId,
				Writer.Envelope,
				false,
				streamId,
				expectedVersion,
				events,
				principal);

			_pendingRequests.CaptureCallback(corrId, action);

			_writerQueueSet.AddToQueue(key, message);

			WorkQueue(key);
			return corrId;
		}

		private void WorkQueue(Guid key) {
			if (_writerQueueSet.IsBusy(key)) return;
			if (!_writerQueueSet.HasPendingWrites(key)) return;
			var write = _writerQueueSet.Dequeue(key);
			if (write != null) {
				Writer.Publish(write, (msg) => Handle(key, msg));
			}
		}

		private void Handle(Guid key, ClientMessage.WriteEventsCompleted message) {
			_writerQueueSet.Finish(key);

			_pendingRequests.CompleteRequest(message);

			WorkQueue(key);
		}

		public Guid WriteEvent(
			string streamId,
			long expectedVersion,
			Event @event,
			IPrincipal principal,
			Action<ClientMessage.WriteEventsCompleted> action) {
			var corrId = Guid.NewGuid();
			return
				Writer.Publish(
					new ClientMessage.WriteEvents(
						corrId,
						corrId,
						Writer.Envelope,
						false,
						streamId,
						expectedVersion,
						new[] {@event},
						principal),
					action);
		}

		public Guid DeleteStream(
			string streamId,
			long expectedVersion,
			bool hardDelete,
			IPrincipal principal,
			Action<ClientMessage.DeleteStreamCompleted> action) {
			var corrId = Guid.NewGuid();
			return StreamDeleter.Publish(
				new ClientMessage.DeleteStream(
					corrId,
					corrId,
					Writer.Envelope,
					false,
					streamId,
					expectedVersion,
					hardDelete,
					principal),
				action);
		}

		public void SubscribeAwake(
			string streamId,
			TFPos from,
			Action<IODispatcherDelayedMessage> action,
			Guid? correlationId = null) {
			var corrId = correlationId ?? Guid.NewGuid();
			Awaker.Publish(
				new AwakeServiceMessage.SubscribeAwake(
					Awaker.Envelope,
					corrId,
					streamId,
					from,
					new IODispatcherDelayedMessage(corrId, null)),
				action);
		}

		public void UnsubscribeAwake(Guid correlationId) {
			Awaker.Cancel(correlationId);
		}

		public void UpdateStreamAcl(
			string streamId,
			long expectedVersion,
			IPrincipal principal,
			StreamMetadata metadata,
			Action<ClientMessage.WriteEventsCompleted> completed) {
			WriteEvents(
				SystemStreams.MetastreamOf(streamId),
				expectedVersion,
				new[] {new Event(Guid.NewGuid(), SystemEventTypes.StreamMetadata, true, metadata.ToJsonBytes(), null)},
				principal,
				completed);
		}

		public void Delay(TimeSpan delay, Action action, Guid? _messageCorrelationId = null) {
			_publisher.Publish(
				TimerMessage.Schedule.Create(
					delay,
					_inputQueueEnvelope,
					new IODispatcherDelayedMessage(_selfId, action, _messageCorrelationId)));
		}

		public void Handle(IODispatcherDelayedMessage message) {
			if (_selfId != message.CorrelationId)
				return;
			message.Action();
		}
	}
}
