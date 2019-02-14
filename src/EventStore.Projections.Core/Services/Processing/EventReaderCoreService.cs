using System;
using System.Collections.Generic;
using System.Diagnostics;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class EventReaderCoreService :
		IHandle<ReaderCoreServiceMessage.StartReader>,
		IHandle<ReaderCoreServiceMessage.StopReader>,
		IHandle<ReaderSubscriptionManagement.Subscribe>,
		IHandle<ReaderSubscriptionManagement.Unsubscribe>,
		IHandle<ReaderSubscriptionManagement.Pause>,
		IHandle<ReaderSubscriptionManagement.Resume>,
		IHandle<ReaderSubscriptionManagement.SpoolStreamReadingCore>,
		IHandle<ReaderSubscriptionManagement.CompleteSpooledStreamReading>,
		IHandle<ReaderSubscriptionMessage.CommittedEventDistributed>,
		IHandle<ReaderSubscriptionMessage.EventReaderIdle>,
		IHandle<ReaderSubscriptionMessage.EventReaderStarting>,
		IHandle<ReaderSubscriptionMessage.EventReaderNotAuthorized>,
		IHandle<ReaderSubscriptionMessage.EventReaderEof>,
		IHandle<ReaderSubscriptionMessage.EventReaderPartitionEof>,
		IHandle<ReaderSubscriptionMessage.EventReaderPartitionDeleted>,
		IHandle<ReaderSubscriptionMessage.EventReaderPartitionMeasured>,
		IHandle<ReaderSubscriptionMessage.Faulted>,
		IHandle<ReaderCoreServiceMessage.ReaderTick> {
		private readonly IPublisher _publisher;
		private readonly IODispatcher _ioDispatcher;
		private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionCoreService>();
		private bool _stopped = true;

		private readonly Dictionary<Guid, IReaderSubscription> _subscriptions =
			new Dictionary<Guid, IReaderSubscription>();

		private readonly Dictionary<Guid, IEventReader> _eventReaders = new Dictionary<Guid, IEventReader>();

		private readonly Dictionary<Guid, Guid> _subscriptionEventReaders = new Dictionary<Guid, Guid>();
		private readonly Dictionary<Guid, Guid> _eventReaderSubscriptions = new Dictionary<Guid, Guid>();
		private readonly HashSet<Guid> _pausedSubscriptions = new HashSet<Guid>();
		private readonly HeadingEventReader _headingEventReader;
		private readonly ICheckpoint _writerCheckpoint;
		private readonly bool _runHeadingReader;
		private readonly bool _faultOutOfOrderProjections;

		public EventReaderCoreService(
			IPublisher publisher, IODispatcher ioDispatcher, int eventCacheSize,
			ICheckpoint writerCheckpoint, bool runHeadingReader, bool faultOutOfOrderProjections) {
			_publisher = publisher;
			_ioDispatcher = ioDispatcher;
			if (runHeadingReader)
				_headingEventReader = new HeadingEventReader(eventCacheSize, _publisher);
			_writerCheckpoint = writerCheckpoint;
			_runHeadingReader = runHeadingReader;
			_faultOutOfOrderProjections = faultOutOfOrderProjections;
		}

		public void Handle(ReaderSubscriptionManagement.Pause message) {
			if (!_pausedSubscriptions.Add(message.SubscriptionId))
				throw new InvalidOperationException("Already paused projection");

			IReaderSubscription projectionSubscription;
			if (!_subscriptions.TryGetValue(message.SubscriptionId, out projectionSubscription))
				return; // may be already unsubscribed when self-unsubscribing

			var eventReaderId = _subscriptionEventReaders[message.SubscriptionId];
			if (eventReaderId == Guid.Empty) // head
			{
				_subscriptionEventReaders.Remove(message.SubscriptionId);
				_headingEventReader.Unsubscribe(message.SubscriptionId);
				var forkedEventReaderId = Guid.NewGuid();
				var forkedEventReader = projectionSubscription.CreatePausedEventReader(
					_publisher, _ioDispatcher, forkedEventReaderId);
				_subscriptionEventReaders.Add(message.SubscriptionId, forkedEventReaderId);
				_eventReaderSubscriptions.Add(forkedEventReaderId, message.SubscriptionId);
				_eventReaders.Add(forkedEventReaderId, forkedEventReader);
				_publisher.Publish(
					new EventReaderSubscriptionMessage.ReaderAssignedReader(
						message.SubscriptionId, forkedEventReaderId));
			} else {
				_eventReaders[eventReaderId].Pause();
			}
		}

		public void Handle(ReaderSubscriptionManagement.Resume message) {
			if (!_pausedSubscriptions.Remove(message.SubscriptionId))
				throw new InvalidOperationException("Not a paused projection");
			var eventReader = _subscriptionEventReaders[message.SubscriptionId];
			_eventReaders[eventReader].Resume();
		}

		public void Handle(ReaderSubscriptionManagement.Subscribe message) {
			if (_stopped)
				return;

			var fromCheckpointTag = message.FromPosition;
			var subscriptionId = message.SubscriptionId;
			var projectionSubscription = message.ReaderStrategy.CreateReaderSubscription(
				_publisher, fromCheckpointTag, message.SubscriptionId, message.Options);
			_subscriptions.Add(subscriptionId, projectionSubscription);

			var distibutionPointCorrelationId = Guid.NewGuid();
			var eventReader = projectionSubscription.CreatePausedEventReader(
				_publisher, _ioDispatcher, distibutionPointCorrelationId);
//            _logger.Trace(
//                "The '{subscriptionId}' projection subscribed to the '{distibutionPointCorrelationId}' distribution point", subscriptionId,
//                distibutionPointCorrelationId);
			_eventReaders.Add(distibutionPointCorrelationId, eventReader);
			_subscriptionEventReaders.Add(subscriptionId, distibutionPointCorrelationId);
			_eventReaderSubscriptions.Add(distibutionPointCorrelationId, subscriptionId);
			_publisher.Publish(
				new EventReaderSubscriptionMessage.ReaderAssignedReader(
					subscriptionId, distibutionPointCorrelationId));
			eventReader.Resume();
		}

		public void Handle(ReaderSubscriptionManagement.Unsubscribe message) {
			if (!_pausedSubscriptions.Contains(message.SubscriptionId))
				Handle(new ReaderSubscriptionManagement.Pause(message.SubscriptionId));
			var eventReaderId = Guid.Empty;
			_subscriptionEventReaders.TryGetValue(message.SubscriptionId, out eventReaderId);
			if (eventReaderId != Guid.Empty) {
				_eventReaders[eventReaderId].Dispose();
				_eventReaders.Remove(eventReaderId);
				_eventReaderSubscriptions.Remove(eventReaderId);
				_publisher.Publish(
					new EventReaderSubscriptionMessage.ReaderAssignedReader(message.SubscriptionId, Guid.Empty));
			}

			_pausedSubscriptions.Remove(message.SubscriptionId);
			_subscriptionEventReaders.Remove(message.SubscriptionId);
			_subscriptions.Remove(message.SubscriptionId);
		}

		public void Handle(ReaderSubscriptionMessage.CommittedEventDistributed message) {
			Guid projectionId;
			if (_stopped)
				return;
			if (_runHeadingReader && _headingEventReader.Handle(message))
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
				return; // unsubscribed

			if (TrySubscribeHeadingEventReader(message, projectionId))
				return;
			if (message.Data != null) {
				try {
					_subscriptions[projectionId].Handle(message);
				} catch (Exception ex) {
					var subscription = _subscriptions[projectionId];
					Handle(new ReaderSubscriptionManagement.Unsubscribe(subscription.SubscriptionId));
					_publisher.Publish(new EventReaderSubscriptionMessage.Failed(subscription.SubscriptionId,
						string.Format("The subscription failed to handle an event {0}:{1}@{2} because {3}",
							message.Data.EventStreamId, message.Data.EventType, message.Data.EventSequenceNumber,
							ex.Message)));
				}
			}
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderIdle message) {
			Guid projectionId;
			if (_stopped)
				return;
			if (_runHeadingReader && _headingEventReader.Handle(message))
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
				return; // unsubscribed
			_subscriptions[projectionId].Handle(message);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderStarting message) {
			Guid projectionId;
			if (_stopped)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
				return; // unsubscribed
			_subscriptions[projectionId].Handle(message);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderEof message) {
			Guid projectionId;
			if (_stopped)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
				return; // unsubscribed
			_subscriptions[projectionId].Handle(message);

//            _pausedSubscriptions.Add(projectionId); // it is actually disposed -- workaround
//            Handle(new ReaderSubscriptionManagement.Unsubscribe(projectionId));
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderPartitionEof message) {
			Guid projectionId;
			if (_stopped)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
				return; // unsubscribed
			_subscriptions[projectionId].Handle(message);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderPartitionDeleted message) {
			Guid projectionId;
			if (_stopped)
				return;
			if (_runHeadingReader && _headingEventReader.Handle(message))
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
				return; // unsubscribed
			_subscriptions[projectionId].Handle(message);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderPartitionMeasured message) {
			Guid projectionId;
			if (_stopped)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
				return; // unsubscribed
			_subscriptions[projectionId].Handle(message);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderNotAuthorized message) {
			Guid projectionId;
			if (_stopped)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
				return; // unsubscribed
			_subscriptions[projectionId].Handle(message);

			_pausedSubscriptions.Add(projectionId); // it is actually disposed -- workaround
			Handle(new ReaderSubscriptionManagement.Unsubscribe(projectionId));
		}

		public void Handle(ReaderSubscriptionMessage.Faulted message) {
			Guid projectionId;
			if (_stopped)
				return;
			if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
				return; // unsubscribed

			if (!_faultOutOfOrderProjections && message.Reason.Contains("was expected in the stream")) {
				// Log without fault the projection
				_logger.Trace(message.Reason);
				return;
			}

			var subscription = _subscriptions[projectionId];
			Handle(new ReaderSubscriptionManagement.Unsubscribe(subscription.SubscriptionId));
			_publisher.Publish(new EventReaderSubscriptionMessage.Failed(subscription.SubscriptionId, message.Reason));
		}

		private void StartReaders() {
			//TODO: do we need to clear subscribed projections here?
			//TODO: do we need to clear subscribed distribution points here?
			_stopped = false;
			var distributionPointCorrelationId = Guid.NewGuid();
			var transactionFileReader = new TransactionFileEventReader(
				_publisher,
				distributionPointCorrelationId,
				SystemAccount.Principal,
				new TFPos(_writerCheckpoint.Read(), -1),
				new RealTimeProvider(),
				deliverEndOfTFPosition: false);

			_eventReaders.Add(distributionPointCorrelationId, transactionFileReader);
			if (_runHeadingReader)
				_headingEventReader.Start(distributionPointCorrelationId, transactionFileReader);
		}

		private void StopReaders() {
			if (_subscriptions.Count > 0) {
				_logger.Info("_subscriptions is not empty after all the projections have been killed");
				_subscriptions.Clear();
			}

			if (_eventReaders.Count > 0) {
				_logger.Info("_eventReaders is not empty after all the projections have been killed");
				_eventReaders.Clear();
			}

			if (_subscriptionEventReaders.Count > 0) {
				_logger.Info("_subscriptionEventReaders is not empty after all the projections have been killed");
				_subscriptionEventReaders.Clear();
			}

			if (_eventReaderSubscriptions.Count > 0) {
				_logger.Info("_eventReaderSubscriptions is not empty after all the projections have been killed");
				_eventReaderSubscriptions.Clear();
			}

			if (_runHeadingReader)
				_headingEventReader.Stop();
			_stopped = true;
		}

		private bool TrySubscribeHeadingEventReader(
			ReaderSubscriptionMessage.CommittedEventDistributed message, Guid projectionId) {
			if (message.SafeTransactionFileReaderJoinPosition == null)
				return false;

			if (!_runHeadingReader)
				return false;

			if (_pausedSubscriptions.Contains(projectionId))
				return false;

			var projectionSubscription = _subscriptions[projectionId];

			if (
				!_headingEventReader.TrySubscribe(
					projectionId, projectionSubscription, message.SafeTransactionFileReaderJoinPosition.Value))
				return false;

			Guid eventReaderId = message.CorrelationId;
			_eventReaders[eventReaderId].Dispose();
			_eventReaders.Remove(eventReaderId);
			_eventReaderSubscriptions.Remove(eventReaderId);
			_subscriptionEventReaders[projectionId] = Guid.Empty;
			_publisher.Publish(
				new EventReaderSubscriptionMessage.ReaderAssignedReader(message.CorrelationId, Guid.Empty));
			return true;
		}

		public void Handle(ReaderCoreServiceMessage.StartReader message) {
			StartReaders();
			_publisher.Publish(new ProjectionCoreServiceMessage.SubComponentStarted("EventReaderCoreService"));
		}

		public void Handle(ReaderCoreServiceMessage.StopReader message) {
			StopReaders();
			_publisher.Publish(new ProjectionCoreServiceMessage.SubComponentStopped("EventReaderCoreService"));
		}

		public void Handle(ReaderCoreServiceMessage.ReaderTick message) {
			message.Action();
		}

		public void Handle(ReaderSubscriptionManagement.SpoolStreamReadingCore message) {
			var eventReader = _subscriptionEventReaders[message.SubscriptionId];
			var handler = (IHandle<ReaderSubscriptionManagement.SpoolStreamReadingCore>)_eventReaders[eventReader];
			handler.Handle(message);
		}

		public void Handle(ReaderSubscriptionManagement.CompleteSpooledStreamReading message) {
			var eventReader = _subscriptionEventReaders[message.SubscriptionId];
			var handler =
				(IHandle<ReaderSubscriptionManagement.CompleteSpooledStreamReading>)_eventReaders[eventReader];
			handler.Handle(message);
		}
	}
}
