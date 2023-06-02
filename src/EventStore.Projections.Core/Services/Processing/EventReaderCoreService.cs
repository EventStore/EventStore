using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;
using ILogger = Serilog.ILogger;

namespace EventStore.Projections.Core.Services.Processing {
	public class EventReaderCoreService :
		IHandle<ReaderCoreServiceMessage.InitReaderService>,
		IHandle<ReaderCoreServiceMessage.DisposeReader>,
		IHandle<ReaderSubscriptionManagement.Subscribe>,
		IHandle<ReaderSubscriptionManagement.Unsubscribe>,
		IHandle<ReaderSubscriptionManagement.Pause>,
		IHandle<ReaderSubscriptionManagement.Resume>,
		IHandle<ReaderSubscriptionMessage.CommittedEventDistributed>,
		IHandle<ReaderSubscriptionMessage.EventReaderIdle>,
		IHandle<ReaderSubscriptionMessage.EventReaderStarting>,
		IHandle<ReaderSubscriptionMessage.EventReaderNotAuthorized>,
		IHandle<ReaderSubscriptionMessage.EventReaderEof>,
		IHandle<ReaderSubscriptionMessage.EventReaderPartitionEof>,
		IHandle<ReaderSubscriptionMessage.EventReaderPartitionDeleted>,
		IHandle<ReaderSubscriptionMessage.Faulted>,
		IHandle<ReaderSubscriptionMessage.ReportProgress> {
		public const string SubComponentName = "EventReaderCoreService";

		private readonly IPublisher _publisher;
		private readonly IODispatcher _ioDispatcher;
		private readonly ILogger _logger = Serilog.Log.ForContext<ProjectionCoreService>();
		private bool _disposed = true;

		private readonly Dictionary<Guid, IReaderSubscription> _subscriptions =
			new Dictionary<Guid, IReaderSubscription>();

		private readonly Dictionary<Guid, IEventReader> _eventReaders = new Dictionary<Guid, IEventReader>();

		private readonly Dictionary<Guid, Guid> _subscriptionEventReaders = new Dictionary<Guid, Guid>();
		private readonly Dictionary<Guid, Guid> _eventReaderSubscriptions = new Dictionary<Guid, Guid>();
		private readonly HashSet<Guid> _pausedSubscriptions = new HashSet<Guid>();
		private readonly HeadingEventReader _headingEventReader;
		private readonly ICheckpoint _writerCheckpoint;
		private bool RunHeadingReader => _headingEventReader != null;
		private readonly bool _faultOutOfOrderProjections;
		private readonly IEnvelope _sendToThisEnvelope;
		private Guid _defaultEventReaderId = Guid.Empty;
		private Guid _reportProgressId;
		private TFPos _headingEventReaderContinueFrom = new(-1, -1);

		public EventReaderCoreService(
			IPublisher publisher, IODispatcher ioDispatcher, int eventCacheSize,
			ICheckpoint writerCheckpoint, bool runHeadingReader, bool faultOutOfOrderProjections) {
			_publisher = publisher;
			_ioDispatcher = ioDispatcher;
			if (runHeadingReader)
				_headingEventReader = new HeadingEventReader(eventCacheSize, _publisher);
			_writerCheckpoint = writerCheckpoint;
			_faultOutOfOrderProjections = faultOutOfOrderProjections;
			_sendToThisEnvelope = new SendToThisEnvelope(this);
		}

		public void Handle(ReaderSubscriptionManagement.Pause message) {
			var subscriptionId = message.SubscriptionId;
			if (!_pausedSubscriptions.Add(subscriptionId))
				throw new InvalidOperationException("Already paused projection");

			if (!_subscriptions.TryGetValue(subscriptionId, out var projectionSubscription))
				return; // may be already unsubscribed when self-unsubscribing

			var eventReaderId = _subscriptionEventReaders[subscriptionId];
			if (eventReaderId == Guid.Empty) // head
			{
				_subscriptionEventReaders.Remove(subscriptionId);
				if (IsHeadingEventReaderInitialized()) {
					_headingEventReader.Unsubscribe(subscriptionId);
				}
				eventReaderId = Guid.NewGuid();
				var eventReader = projectionSubscription.CreatePausedEventReader(
					_publisher, _ioDispatcher, eventReaderId);
				_subscriptionEventReaders.Add(subscriptionId, eventReaderId);
				_eventReaderSubscriptions.Add(eventReaderId, subscriptionId);
				_eventReaders.Add(eventReaderId, eventReader);
				_publisher.Publish(
					new EventReaderSubscriptionMessage.ReaderAssignedReader(
						subscriptionId, eventReaderId));
			} else {
				_eventReaders[eventReaderId].Pause();
			}
		}

		public void Handle(ReaderSubscriptionManagement.Resume message) {
			var subscriptionId = message.SubscriptionId;
			if (!_pausedSubscriptions.Remove(subscriptionId))
				throw new InvalidOperationException("Not a paused projection");
			var eventReader = _subscriptionEventReaders[subscriptionId];
			_eventReaders[eventReader].Resume();
		}

		public void Handle(ReaderSubscriptionManagement.Subscribe message) {
			if (_disposed)
				return;
			if (NeedToInitHeadingEventReader(message)) {
				StartReader();
				_logger.Information($"Heading event reader started on thread : {Thread.CurrentThread.Name}");
			}
			var fromCheckpointTag = message.FromPosition;
			var subscriptionId = message.SubscriptionId;
			var projectionSubscription = message.ReaderStrategy.CreateReaderSubscription(
				_publisher, fromCheckpointTag, subscriptionId, message.Options);
			_subscriptions.Add(subscriptionId, projectionSubscription);

			var eventReaderId = Guid.NewGuid();
			var eventReader = projectionSubscription.CreatePausedEventReader(
				_publisher, _ioDispatcher, eventReaderId);
			_eventReaders.Add(eventReaderId, eventReader);
			_subscriptionEventReaders.Add(subscriptionId, eventReaderId);
			_eventReaderSubscriptions.Add(eventReaderId, subscriptionId);
			_publisher.Publish(
				new EventReaderSubscriptionMessage.ReaderAssignedReader(
					subscriptionId, eventReaderId));
			eventReader.Resume();
		}

		public void Handle(ReaderSubscriptionManagement.Unsubscribe message) {
			var subscriptionId = message.SubscriptionId;
			if (!_pausedSubscriptions.Contains(subscriptionId))
				Handle(new ReaderSubscriptionManagement.Pause(subscriptionId));
			_subscriptionEventReaders.TryGetValue(subscriptionId, out var eventReaderId);
			if (eventReaderId != Guid.Empty) {
				_eventReaders[eventReaderId].Dispose();
				_eventReaders.Remove(eventReaderId);
				_eventReaderSubscriptions.Remove(eventReaderId);
				_publisher.Publish(
					new EventReaderSubscriptionMessage.ReaderAssignedReader(subscriptionId, Guid.Empty));
			}

			_pausedSubscriptions.Remove(subscriptionId);
			_subscriptionEventReaders.Remove(subscriptionId);
			_subscriptions.Remove(subscriptionId);

			if (_subscriptions.Count == 0 && IsHeadingEventReaderInitialized()) {
				StopReader();
				_logger.Information($"Heading event reader stopped on thread : {Thread.CurrentThread.Name}");
			}
		}

		public void Handle(ReaderSubscriptionMessage.CommittedEventDistributed message) {
			if (_disposed)
				return;
			if (RunHeadingReader && IsHeadingEventReaderInitialized() && _headingEventReader.Handle(message))
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out var subscriptionId))
				return; // unsubscribed

			if (TrySubscribeHeadingEventReader(message, subscriptionId))
				return;
			if (message.Data != null) {
				try {
					_subscriptions[subscriptionId].Handle(message);
				} catch (Exception ex) {
					var subscription = _subscriptions[subscriptionId];
					Handle(new ReaderSubscriptionManagement.Unsubscribe(subscription.SubscriptionId));
					_publisher.Publish(new EventReaderSubscriptionMessage.Failed(subscription.SubscriptionId,
						string.Format("The subscription failed to handle an event {0}:{1}@{2} because {3}",
							message.Data.EventStreamId, message.Data.EventType, message.Data.EventSequenceNumber,
							ex.Message)));
				}
			}
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderIdle message) {
			if (_disposed)
				return;
			if (RunHeadingReader && IsHeadingEventReaderInitialized() && _headingEventReader.Handle(message))
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out var subscriptionId))
				return; // unsubscribed
			_subscriptions[subscriptionId].Handle(message);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderStarting message) {
			if (_disposed)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out var subscriptionId))
				return; // unsubscribed
			_subscriptions[subscriptionId].Handle(message);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderEof message) {
			if (_disposed)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out var subscriptionId))
				return; // unsubscribed
			_subscriptions[subscriptionId].Handle(message);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderPartitionEof message) {
			if (_disposed)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out var subscriptionId))
				return; // unsubscribed
			_subscriptions[subscriptionId].Handle(message);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderPartitionDeleted message) {
			if (_disposed)
				return;
			if (RunHeadingReader && IsHeadingEventReaderInitialized() && _headingEventReader.Handle(message))
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out var subscriptionId))
				return; // unsubscribed
			_subscriptions[subscriptionId].Handle(message);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderNotAuthorized message) {
			if (_disposed)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out var subscriptionId))
				return; // unsubscribed
			_subscriptions[subscriptionId].Handle(message);

			_pausedSubscriptions.Add(subscriptionId); // it is actually disposed -- workaround
			Handle(new ReaderSubscriptionManagement.Unsubscribe(subscriptionId));
		}

		public void Handle(ReaderSubscriptionMessage.Faulted message) {
			if (_disposed)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out var subscriptionId))
				return; // unsubscribed

			if (!_faultOutOfOrderProjections && message.Reason.Contains("was expected in the stream")) {
				// Log without fault the projection
				_logger.Verbose(message.Reason);
				return;
			}

			var subscription = _subscriptions[subscriptionId];
			Handle(new ReaderSubscriptionManagement.Unsubscribe(subscription.SubscriptionId));
			_publisher.Publish(new EventReaderSubscriptionMessage.Failed(subscription.SubscriptionId, message.Reason));
		}

		public void Handle(ReaderSubscriptionMessage.ReportProgress message) {
			if (_disposed || message.CorrelationId != _reportProgressId)
				return;

			foreach (var subscription in _subscriptions.Values) {
				subscription.Handle(message);
			}

			_reportProgressId = Guid.NewGuid();
			_publisher.Publish(TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(500), _sendToThisEnvelope, new ReaderSubscriptionMessage.ReportProgress(_reportProgressId)));
		}

		private void StartReader() {
			//TODO: do we need to clear subscribed projections here?
			//TODO: do we need to clear subscribed distribution points here?
			_defaultEventReaderId = Guid.NewGuid();
			var from = new TFPos(_writerCheckpoint.Read(), -1);
			// possible because _writerCheckpoint.Read() reads flushed checkpoint; _writerCheckpoint might not have flushed between reader stop-start
			_headingEventReaderContinueFrom = from = from < _headingEventReaderContinueFrom ? _headingEventReaderContinueFrom : from;
			var transactionFileReader = new TransactionFileEventReader(
				_publisher,
				_defaultEventReaderId,
				SystemAccounts.System,
				from,
				new RealTimeProvider(),
				deliverEndOfTFPosition: false);

			_eventReaders.Add(_defaultEventReaderId, transactionFileReader);
			if (RunHeadingReader)
				_headingEventReader.Start(_defaultEventReaderId, transactionFileReader);
		}

		private void StopReader() {
			if (_eventReaders.TryGetValue(_defaultEventReaderId, out var eventReader)) {
				_headingEventReaderContinueFrom = ((TransactionFileEventReader)eventReader).From;
				eventReader.Dispose();
				_eventReaders.Remove(_defaultEventReaderId);
				_eventReaderSubscriptions.Remove(_defaultEventReaderId);
			}

			if (_subscriptions.Count > 0) {
				_logger.Information("_subscriptions is not empty after all the projections have been killed");
				_subscriptions.Clear();
			}

			if (_eventReaders.Count > 0) {
				_logger.Information("_eventReaders is not empty after all the projections have been killed");
				_eventReaders.Clear();
			}

			if (_subscriptionEventReaders.Count > 0) {
				_logger.Information("_subscriptionEventReaders is not empty after all the projections have been killed");
				_subscriptionEventReaders.Clear();
			}

			if (_eventReaderSubscriptions.Count > 0) {
				_logger.Information("_eventReaderSubscriptions is not empty after all the projections have been killed");
				_eventReaderSubscriptions.Clear();
			}

			if (RunHeadingReader && IsHeadingEventReaderInitialized())
				_headingEventReader.Stop();
			_defaultEventReaderId = Guid.Empty;
		}

		private bool TrySubscribeHeadingEventReader(
			ReaderSubscriptionMessage.CommittedEventDistributed message, Guid subscriptionId) {
			if (message.SafeTransactionFileReaderJoinPosition == null)
				return false;

			if (!RunHeadingReader || !IsHeadingEventReaderInitialized())
				return false;

			if (_pausedSubscriptions.Contains(subscriptionId))
				return false;

			var projectionSubscription = _subscriptions[subscriptionId];

			if (
				!_headingEventReader.TrySubscribe(
					subscriptionId, projectionSubscription, message.SafeTransactionFileReaderJoinPosition.Value))
				return false;

			Guid eventReaderId = message.CorrelationId;
			_eventReaders[eventReaderId].Dispose();
			_eventReaders.Remove(eventReaderId);
			_eventReaderSubscriptions.Remove(eventReaderId);
			_subscriptionEventReaders[subscriptionId] = Guid.Empty;
			_publisher.Publish(
				new EventReaderSubscriptionMessage.ReaderAssignedReader(message.CorrelationId, Guid.Empty));
			return true;
		}

		public void Handle(ReaderCoreServiceMessage.InitReaderService message) {
			_disposed = false;
			_reportProgressId = Guid.NewGuid();
			_publisher.Publish(TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(500), _sendToThisEnvelope, new ReaderSubscriptionMessage.ReportProgress(_reportProgressId)));
			_publisher.Publish(new ProjectionCoreServiceMessage.SubComponentStarted(
				SubComponentName, message.InstanceCorrelationId));
		}
		
		public void Handle(ReaderCoreServiceMessage.DisposeReader message) {
			StopReader();
			_disposed = true;
			_publisher.Publish(
				new ProjectionCoreServiceMessage.SubComponentStopped(SubComponentName, message.QueueId));
		}

		private bool IsHeadingEventReaderInitialized() {
			return _defaultEventReaderId != Guid.Empty;
		}

		private bool NeedToInitHeadingEventReader(ReaderSubscriptionManagement.Subscribe subscribe) =>
			//should not init if received subscription request is from an one-time projection because such projections won't ever subscribe to heading event reader
			!IsHeadingEventReaderInitialized() && !subscribe.Options.StopOnEof;
	}
}
