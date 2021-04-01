using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Services.TimerService;
using System.Diagnostics;
using EventStore.Core.Data;
using EventStore.Core.Services.Histograms;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace EventStore.Core.Services.RequestManager {
	public class RequestManagementService :
		IHandle<SystemMessage.SystemInit>,
		IHandle<ClientMessage.WriteEvents>,
		IHandle<ClientMessage.DeleteStream>,
		IHandle<ClientMessage.TransactionStart>,
		IHandle<ClientMessage.TransactionWrite>,
		IHandle<ClientMessage.TransactionCommit>,
		IHandle<StorageMessage.RequestCompleted>,
		IHandle<StorageMessage.AlreadyCommitted>,
		IHandle<StorageMessage.PrepareAck>,
		IHandle<ReplicationTrackingMessage.ReplicatedTo>,
		IHandle<ReplicationTrackingMessage.IndexedTo>,
		IHandle<StorageMessage.CommitIndexed>,
		IHandle<StorageMessage.WrongExpectedVersion>,
		IHandle<StorageMessage.InvalidTransaction>,
		IHandle<StorageMessage.StreamDeleted>,
		IHandle<StorageMessage.RequestManagerTimerTick>,
		IHandle<SystemMessage.StateChangeMessage> {
		private readonly IPublisher _bus;
		private readonly TimerMessage.Schedule _tickRequestMessage;
		private readonly Dictionary<Guid, RequestManagerBase> _currentRequests = new Dictionary<Guid, RequestManagerBase>();
		private readonly Dictionary<Guid, Stopwatch> _currentTimedRequests = new Dictionary<Guid, Stopwatch>();
		private const string _requestManagerHistogram = "request-manager";
		private readonly TimeSpan _prepareTimeout;
		private readonly TimeSpan _commitTimeout;
		private readonly CommitSource _commitSource;
		private VNodeState _nodeState;
		private CommitLevel _commitLevel;
		private Channel<ClientMessage.WriteEvents> _grpcWrites;
		private object _trackerLock = new object();

		public RequestManagementService(
			IPublisher bus,
			TimeSpan prepareTimeout,
			TimeSpan commitTimeout,
			CommitLevel commitLevel,
			int maxWriteConcurrency) {
			Ensure.NotNull(bus, "bus");
			_bus = bus;
			_tickRequestMessage = TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(1000),
				new PublishEnvelope(bus),
				new StorageMessage.RequestManagerTimerTick());

			_prepareTimeout = prepareTimeout;
			_commitTimeout = commitTimeout;
			_commitLevel = commitLevel;
			_commitSource = new CommitSource();
			var channelOptions = new BoundedChannelOptions(maxWriteConcurrency) {
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			};
			_grpcWrites = Channel.CreateBounded<ClientMessage.WriteEvents>(channelOptions);
			Task.Run(() => DequeWrites());
		}
		public void Enque(ClientMessage.WriteEvents @event) {
			if (@event == null) { return; }
			while (!_grpcWrites.Writer.TryWrite(@event)) { }
		}

		private async void DequeWrites() {
			while (true) {
				//We have a dedicated thread, it's faster to keep using it
#pragma warning disable CAC001 // ConfigureAwaitChecker
				Handle(await _grpcWrites.Reader.ReadAsync());
#pragma warning restore CAC001 // ConfigureAwaitChecker
			}
		}
		public void Handle(ClientMessage.WriteEvents message) {
			var manager = new AppendEvents(
								_bus,
								_commitTimeout,
								message.Envelope,
								message.InternalCorrId,
								message.CorrelationId,
								message.EventStreamId,
								message.ExpectedVersion,
								message.Events,
								_commitSource,
								_commitLevel,
								message.CancellationToken);
			lock (_trackerLock) {
				_currentRequests.Add(message.InternalCorrId, manager);
				_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
			}
			manager.Start();
		}

		public void Handle(ClientMessage.DeleteStream message) {
			var manager = new DeleteStream(
								_bus,
								_commitTimeout,
								message.Envelope,
								message.InternalCorrId,
								message.CorrelationId,
								message.EventStreamId,
								message.ExpectedVersion,
								message.HardDelete,
								_commitSource,
								message.CancellationToken);
			lock (_trackerLock) {
				_currentRequests.Add(message.InternalCorrId, manager);
				_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
			}
			manager.Start();
		}

		public void Handle(ClientMessage.TransactionStart message) {
			var manager = new TransactionStart(
								_bus,
								_prepareTimeout,
								message.Envelope,
								message.InternalCorrId,
								message.CorrelationId,
								message.EventStreamId,
								message.ExpectedVersion,
								_commitSource);
			lock (_trackerLock) {
				_currentRequests.Add(message.InternalCorrId, manager);
				_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
			}
			manager.Start();
		}

		public void Handle(ClientMessage.TransactionWrite message) {
			var manager = new TransactionWrite(
								_bus,
								_prepareTimeout,
								message.Envelope,
								message.InternalCorrId,
								message.CorrelationId,
								message.Events,
								message.TransactionId,
								_commitSource);
			lock (_trackerLock) {
				_currentRequests.Add(message.InternalCorrId, manager);
				_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
			}
			manager.Start();
		}

		public void Handle(ClientMessage.TransactionCommit message) {
			var manager = new TransactionCommit(
								_bus,
								_prepareTimeout,
								_commitTimeout,
								message.Envelope,
								message.InternalCorrId,
								message.CorrelationId,
								message.TransactionId,
								_commitSource);
			lock (_trackerLock) {
				_currentRequests.Add(message.InternalCorrId, manager);
				_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
			}
			manager.Start();
		}


		public void Handle(SystemMessage.StateChangeMessage message) {
			//TODO(clc): if we have become resigning leader should all requests be actively disposed?
			_nodeState = message.State;
		}

		public void Handle(SystemMessage.SystemInit message) {
			_bus.Publish(_tickRequestMessage);
		}

		public void Handle(StorageMessage.RequestManagerTimerTick message) {
			List<RequestManagerBase> requests;
			lock (_trackerLock) {
				requests = new List<RequestManagerBase>(_currentRequests.Values);
			}
			foreach (var currentRequest in requests) {
				currentRequest.Handle(message);
			}
			//TODO(clc): if we have become resigning leader should all requests be actively disposed?
			if (_nodeState == VNodeState.ResigningLeader) {
				lock (_trackerLock) {
					if (_currentRequests.Count == 0) {
						_bus.Publish(new SystemMessage.RequestQueueDrained());
					}
				}
			}

			_bus.Publish(_tickRequestMessage);
		}

		public void Handle(StorageMessage.RequestCompleted message) {
			Stopwatch watch = null;
			lock (_trackerLock) {
				if (_currentTimedRequests.TryGetValue(message.CorrelationId, out watch)) {
					HistogramService.SetValue(_requestManagerHistogram,
						(long)((((double)watch.ElapsedTicks) / Stopwatch.Frequency) * 1000000000));
					_currentTimedRequests.Remove(message.CorrelationId);
				}

				if (!_currentRequests.Remove(message.CorrelationId))
					throw new InvalidOperationException("Should never complete request twice.");
			}
		}

		public void Handle(ReplicationTrackingMessage.ReplicatedTo message) => _commitSource.Handle(message);
		public void Handle(ReplicationTrackingMessage.IndexedTo message) => _commitSource.Handle(message);

		public void Handle(StorageMessage.AlreadyCommitted message) => DispatchInternal(message.CorrelationId, message);
		public void Handle(StorageMessage.PrepareAck message) => DispatchInternal(message.CorrelationId, message);
		public void Handle(StorageMessage.CommitIndexed message) => DispatchInternal(message.CorrelationId, message);
		public void Handle(StorageMessage.WrongExpectedVersion message) => DispatchInternal(message.CorrelationId, message);
		public void Handle(StorageMessage.InvalidTransaction message) => DispatchInternal(message.CorrelationId, message);
		public void Handle(StorageMessage.StreamDeleted message) => DispatchInternal(message.CorrelationId, message);

		private void DispatchInternal<T>(Guid correlationId, T message) where T : Message {
			lock (_trackerLock) {
				if (_currentRequests.TryGetValue(correlationId, out var manager)) {
					var x = manager as IHandle<T>;
					x?.Handle(message);
				}
			}
		}


	}
}
