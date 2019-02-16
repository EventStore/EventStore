using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;

namespace EventStore.Core.Services.PersistentSubscription {
	//TODO GFY REFACTOR TO USE ACTUAL STATE MACHINE
	public class PersistentSubscription {
		private static readonly ILogger Log = LogManager.GetLoggerFor<PersistentSubscription>();

		public string SubscriptionId {
			get { return _settings.SubscriptionId; }
		}

		public string EventStreamId {
			get { return _settings.EventStreamId; }
		}

		public string GroupName {
			get { return _settings.GroupName; }
		}

		public bool ResolveLinkTos {
			get { return _settings.ResolveLinkTos; }
		}

		internal PersistentSubscriptionClientCollection _pushClients;
		private readonly PersistentSubscriptionStats _statistics;
		private readonly Stopwatch _totalTimeWatch;
		private readonly OutstandingMessageCache _outstandingMessages;
		internal StreamBuffer _streamBuffer;
		private PersistentSubscriptionState _state = PersistentSubscriptionState.NotReady;
		private long _nextEventToPullFrom;
		private long _lastCheckPoint;
		private DateTime _lastCheckPointTime = DateTime.MinValue;
		private readonly PersistentSubscriptionParams _settings;
		private long _lastKnownMessage = -1;
		private readonly object _lock = new object();

		public bool HasClients {
			get { return _pushClients.Count > 0; }
		}

		public int ClientCount {
			get { return _pushClients.Count; }
		}

		public bool HasReachedMaxClientCount {
			get { return _settings.MaxSubscriberCount != 0 && _pushClients.Count >= _settings.MaxSubscriberCount; }
		}

		public PersistentSubscriptionState State {
			get { return _state; }
		}

		public int OutstandingMessageCount {
			get { return _outstandingMessages.Count; } //use outstanding not connections as http shows up here too
		}

		public PersistentSubscription(PersistentSubscriptionParams persistentSubscriptionParams) {
			Ensure.NotNull(persistentSubscriptionParams.StreamReader, "eventLoader");
			Ensure.NotNull(persistentSubscriptionParams.CheckpointReader, "checkpointReader");
			Ensure.NotNull(persistentSubscriptionParams.CheckpointWriter, "checkpointWriter");
			Ensure.NotNull(persistentSubscriptionParams.MessageParker, "messageParker");
			Ensure.NotNull(persistentSubscriptionParams.SubscriptionId, "subscriptionId");
			Ensure.NotNull(persistentSubscriptionParams.EventStreamId, "eventStreamId");
			Ensure.NotNull(persistentSubscriptionParams.GroupName, "groupName");
			_nextEventToPullFrom = 0;
			_totalTimeWatch = new Stopwatch();
			_settings = persistentSubscriptionParams;
			_totalTimeWatch.Start();
			_statistics = new PersistentSubscriptionStats(this, _settings, _totalTimeWatch);
			_outstandingMessages = new OutstandingMessageCache();
			InitAsNew();
		}

		public void InitAsNew() {
			_state = PersistentSubscriptionState.NotReady;
			_lastCheckPoint = -1;
			_lastKnownMessage = -1;
			_statistics.SetLastKnownEventNumber(-1);
			_settings.CheckpointReader.BeginLoadState(SubscriptionId, OnCheckpointLoaded);

			_pushClients = new PersistentSubscriptionClientCollection(_settings.ConsumerStrategy);
		}

		private void OnCheckpointLoaded(long? checkpoint) {
			lock (_lock) {
				_state = PersistentSubscriptionState.Behind;
				if (!checkpoint.HasValue) {
					Log.Debug("Subscription {subscriptionId}: no checkpoint found", _settings.SubscriptionId);

					Log.Debug("Start from = " + _settings.StartFrom);
					_nextEventToPullFrom = _settings.StartFrom >= 0 ? _settings.StartFrom : 0;
					_streamBuffer = new StreamBuffer(_settings.BufferSize, _settings.LiveBufferSize, -1,
						_settings.StartFrom >= 0);
					TryReadingNewBatch();
				} else {
					_nextEventToPullFrom = checkpoint.Value + 1;
					Log.Debug("Subscription {subscriptionId}: read checkpoint {checkpoint}", _settings.SubscriptionId,
						checkpoint.Value);
					_streamBuffer = new StreamBuffer(_settings.BufferSize, _settings.LiveBufferSize, -1, true);
					TryReadingNewBatch();
				}
			}
		}

		public void TryReadingNewBatch() {
			lock (_lock) {
				if ((_state & PersistentSubscriptionState.OutstandingPageRequest) > 0) return;
				if (_streamBuffer.Live) {
					SetLive();
					return;
				}

				if (!_streamBuffer.CanAccept(_settings.ReadBatchSize)) return;
				_state |= PersistentSubscriptionState.OutstandingPageRequest;
				_settings.StreamReader.BeginReadEvents(_settings.EventStreamId, _nextEventToPullFrom,
					Math.Max(_settings.ReadBatchSize, 10), _settings.ReadBatchSize, _settings.ResolveLinkTos,
					HandleReadCompleted);
			}
		}

		private void SetLive() {
			//TODO GFY this is hacky and just trying to keep the state at this level when it
			//lives in the streambuffer its for reporting reasons and likely should be revisited
			//at some point.
			_state &= ~PersistentSubscriptionState.Behind;
			_state |= PersistentSubscriptionState.Live;
		}

		private void SetBehind() {
			_state |= PersistentSubscriptionState.Behind;
			_state &= ~PersistentSubscriptionState.Live;
		}

		public void HandleReadCompleted(ResolvedEvent[] events, long newposition, bool isEndOfStream) {
			lock (_lock) {
				if ((_state & PersistentSubscriptionState.OutstandingPageRequest) == 0) return;
				_state &= ~PersistentSubscriptionState.OutstandingPageRequest;
				if (_streamBuffer.Live) return;
				foreach (var ev in events) {
					_streamBuffer.AddReadMessage(new OutstandingMessage(ev.OriginalEvent.EventId, null, ev, 0));
				}

				if (events.Length > 0) {
					_statistics.SetLastKnownEventNumber(events[events.Length - 1].OriginalEventNumber);
				}

				if (_streamBuffer.Live) {
					SetLive();
				}

				if (isEndOfStream) {
					if (_streamBuffer.TryMoveToLive()) {
						SetLive();
						return;
					}
				}

				_nextEventToPullFrom = newposition;
				TryReadingNewBatch();
				TryPushingMessagesToClients();
			}
		}

		private void TryPushingMessagesToClients() {
			lock (_lock) {
				if (_state == PersistentSubscriptionState.NotReady) return;

				foreach (StreamBuffer.OutstandingMessagePointer messagePointer in _streamBuffer.Scan()) {
					OutstandingMessage message = messagePointer.Message;
					ConsumerPushResult result =
						_pushClients.PushMessageToClient(message.ResolvedEvent, message.RetryCount);
					if (result == ConsumerPushResult.Sent) {
						messagePointer.MarkSent();
						MarkBeginProcessing(message);
						_lastKnownMessage = Math.Max(_lastKnownMessage, message.ResolvedEvent.OriginalEventNumber);
					} else if (result == ConsumerPushResult.Skipped) {
						// The consumer strategy skipped the message so leave it in the buffer and continue.
					} else if (result == ConsumerPushResult.NoMoreCapacity) {
						return;
					}
				}
			}
		}

		public void NotifyLiveSubscriptionMessage(ResolvedEvent resolvedEvent) {
			lock (_lock) {
				if (resolvedEvent.OriginalEvent.EventNumber < _settings.StartFrom) return;
				if (_state == PersistentSubscriptionState.NotReady) return;
				_statistics.SetLastKnownEventNumber(resolvedEvent.OriginalEventNumber);
				var waslive = _streamBuffer.Live; //hacky
				_streamBuffer.AddLiveMessage(new OutstandingMessage(resolvedEvent.OriginalEvent.EventId, null,
					resolvedEvent, 0));
				if (!_streamBuffer.Live) {
					SetBehind();
					if (waslive) _nextEventToPullFrom = resolvedEvent.OriginalEventNumber;
				}

				TryPushingMessagesToClients();
			}
		}

		public IEnumerable<ResolvedEvent> GetNextNOrLessMessages(int count) {
			lock (_lock) {
				foreach (var messagePointer in _streamBuffer.Scan().Take(count)) {
					messagePointer.MarkSent();
					MarkBeginProcessing(messagePointer.Message);
					_lastKnownMessage = Math.Max(_lastKnownMessage,
						messagePointer.Message.ResolvedEvent.OriginalEventNumber);
					yield return messagePointer.Message.ResolvedEvent;
				}
			}
		}

		private void MarkBeginProcessing(OutstandingMessage message) {
			_statistics.IncrementProcessed();
			StartMessage(message,
				_settings.MessageTimeout == TimeSpan.MaxValue
					? DateTime.MaxValue
					: DateTime.UtcNow + _settings.MessageTimeout);
		}

		public void AddClient(Guid correlationId, Guid connectionId, IEnvelope envelope, int maxInFlight, string user,
			string @from) {
			lock (_lock) {
				var client = new PersistentSubscriptionClient(correlationId, connectionId, envelope, maxInFlight, user,
					@from, _totalTimeWatch, _settings.ExtraStatistics);
				_pushClients.AddClient(client);
				TryPushingMessagesToClients();
			}
		}

		public void Shutdown() {
			lock (_lock) {
				_pushClients.ShutdownAll();
			}
		}

		public void RemoveClientByConnectionId(Guid connectionId) {
			lock (_lock) {
				var lostMessages =
					_pushClients.RemoveClientByConnectionId(connectionId).OrderBy(v => v.OriginalEventNumber);
				foreach (var m in lostMessages) {
					RetryMessage(m, 0);
				}

				TryPushingMessagesToClients();
			}
		}

		public void RemoveClientByCorrelationId(Guid correlationId, bool sendDropNotification) {
			lock (_lock) {
				var lostMessages = _pushClients.RemoveClientByCorrelationId(correlationId, sendDropNotification)
					.OrderBy(v => v.OriginalEventNumber);
				foreach (var m in lostMessages) {
					RetryMessage(m, 0);
				}

				TryPushingMessagesToClients();
			}
		}

		public void TryMarkCheckpoint(bool isTimeCheck) {
			lock (_lock) {
				var lowest = _outstandingMessages.GetLowestPosition() - 1;
				// Subtract 1 from retry and outstanding as those messages have not been processed yet.
				var lowestBufferedRetry = _streamBuffer.GetLowestRetry() - 1;
				lowest = Math.Min(lowest, lowestBufferedRetry);
				if (lowest == long.MaxValue - 1) lowest = _lastKnownMessage;
				if (lowest == -1) return;

				//no outstanding messages. in this case we can say that the last known
				//event would be our checkpoint place (we have already completed it)
				var difference = lowest - _lastCheckPoint;
				var now = DateTime.UtcNow;
				var timedifference = now - _lastCheckPointTime;
				if (timedifference < _settings.CheckPointAfter && difference < _settings.MaxCheckPointCount) return;
				if ((difference >= _settings.MinCheckPointCount && isTimeCheck) ||
				    difference >= _settings.MaxCheckPointCount) {
					_lastCheckPointTime = now;
					_lastCheckPoint = lowest;
					_settings.CheckpointWriter.BeginWriteState(lowest);
					_statistics.SetLastCheckPoint(lowest);
				}
			}
		}

		public void AddMessageAsProcessing(ResolvedEvent ev, PersistentSubscriptionClient client) {
			lock (_lock) {
				StartMessage(new OutstandingMessage(ev.OriginalEvent.EventId, client, ev, 0),
					_settings.MessageTimeout == TimeSpan.MaxValue
						? DateTime.MaxValue
						: DateTime.UtcNow + _settings.MessageTimeout);
			}
		}

		public void AcknowledgeMessagesProcessed(Guid correlationId, Guid[] processedEventIds) {
			lock (_lock) {
				RemoveProcessingMessages(correlationId, processedEventIds);
				TryMarkCheckpoint(false);
				TryReadingNewBatch();
				TryPushingMessagesToClients();
			}
		}

		public void NotAcknowledgeMessagesProcessed(Guid correlationId, Guid[] processedEventIds, NakAction action,
			string reason) {
			lock (_lock) {
				foreach (var id in processedEventIds) {
					Log.Info("Message NAK'ed id {id} action to take {action} reason '{reason}'", id, action,
						reason ?? "");
					HandleNackedMessage(action, id, reason);
				}

				RemoveProcessingMessages(correlationId, processedEventIds);
				TryMarkCheckpoint(false);
				TryReadingNewBatch();
				TryPushingMessagesToClients();
			}
		}

		private void HandleNackedMessage(NakAction action, Guid id, string reason) {
			OutstandingMessage e;
			switch (action) {
				case NakAction.Retry:
				case NakAction.Unknown:
					if (_outstandingMessages.GetMessageById(id, out e)) {
						if (!ActionTakenForRetriedMessage(e)) {
							RetryMessage(e.ResolvedEvent, e.RetryCount);
						}
					}

					break;
				case NakAction.Park:
					if (_outstandingMessages.GetMessageById(id, out e)) {
						ParkMessage(e.ResolvedEvent, "Client explicitly NAK'ed message.\n" + reason, 0);
					}

					break;
				case NakAction.Stop:
					StopSubscription();
					break;
				case NakAction.Skip:
					SkipMessage(id);
					break;
				default:
					SkipMessage(id);
					break;
			}
		}

		private void ParkMessage(ResolvedEvent resolvedEvent, string reason, int count) {
			_settings.MessageParker.BeginParkMessage(resolvedEvent, reason, (e, result) => {
				if (result != OperationResult.Success) {
					if (count < 5) {
						Log.Info("Unable to park message {stream}/{eventNumber} operation failed {e} retrying",
							e.OriginalStreamId,
							e.OriginalEventNumber, result);
						ParkMessage(e, reason, count + 1);
						return;
					}

					Log.Error(
						"Unable to park message {stream}/{eventNumber} operation failed {e} after retries. Possible message loss",
						e.OriginalStreamId,
						e.OriginalEventNumber, result);
				}

				lock (_lock) {
					_outstandingMessages.Remove(e.OriginalEvent.EventId);
					_pushClients.RemoveProcessingMessage(e.OriginalEvent.EventId);
					TryPushingMessagesToClients();
				}
			});
		}

		public void RetryAllParkedMessages() {
			lock (_lock) {
				if ((_state & PersistentSubscriptionState.ReplayingParkedMessages) > 0) return; //already replaying
				_state |= PersistentSubscriptionState.ReplayingParkedMessages;
				_settings.MessageParker.BeginReadEndSequence(end => {
					if (!end.HasValue) {
						_state ^= PersistentSubscriptionState.ReplayingParkedMessages;
						return; //nothing to do.
					}

					TryReadingParkedMessagesFrom(0, end.Value + 1);
				});
			}
		}

		private void TryReadingParkedMessagesFrom(long position, long stopAt) {
			if ((_state & PersistentSubscriptionState.ReplayingParkedMessages) == 0) return; //not replaying

			Ensure.Positive(stopAt - position, "count");

			var count = (int)Math.Min(stopAt - position, _settings.ReadBatchSize);
			_settings.StreamReader.BeginReadEvents(_settings.ParkedMessageStream, position, count,
				_settings.ReadBatchSize, true,
				(events, newposition, isstop) => HandleParkedReadCompleted(events, newposition, isstop, stopAt));
		}

		public void HandleParkedReadCompleted(ResolvedEvent[] events, long newposition, bool isEndofStrem,
			long stopAt) {
			lock (_lock) {
				if ((_state & PersistentSubscriptionState.ReplayingParkedMessages) == 0) return;

				foreach (var ev in events) {
					if (ev.OriginalEventNumber == stopAt) {
						break;
					}

					Log.Debug("Retrying event {eventId} {stream}/{eventNumber} on subscription {subscriptionId}",
						ev.OriginalEvent.EventId, ev.OriginalStreamId, ev.OriginalEventNumber,
						_settings.SubscriptionId);
					_streamBuffer.AddRetry(new OutstandingMessage(ev.OriginalEvent.EventId, null, ev, 0));
				}

				TryPushingMessagesToClients();

				if (isEndofStrem || stopAt <= newposition) {
					var replayedEnd = newposition == -1 ? stopAt : Math.Min(stopAt, newposition);
					_settings.MessageParker.BeginMarkParkedMessagesReprocessed(replayedEnd);
					_state ^= PersistentSubscriptionState.ReplayingParkedMessages;
				} else {
					TryReadingParkedMessagesFrom(newposition, stopAt);
				}
			}
		}

		private void StartMessage(OutstandingMessage message, DateTime expires) {
			var result = _outstandingMessages.StartMessage(message, expires);

			if (result == StartMessageResult.SkippedDuplicate) {
				Log.Warn("Skipping message {stream}/{eventNumber} with duplicate eventId {eventId}",
					message.ResolvedEvent.OriginalStreamId,
					message.ResolvedEvent.OriginalEventNumber,
					message.EventId);
			}
		}

		private void SkipMessage(Guid id) {
			_outstandingMessages.Remove(id);
		}

		private void StopSubscription() {
			//TODO CC Stop subscription?
		}

		private void RemoveProcessingMessages(Guid correlationId, Guid[] processedEventIds) {
			_pushClients.RemoveProcessingMessages(correlationId, processedEventIds);
			foreach (var id in processedEventIds) {
				_outstandingMessages.Remove(id);
			}
		}

		public void NotifyClockTick(DateTime time) {
			lock (_lock) {
				if (_state == PersistentSubscriptionState.NotReady) return;

				foreach (var message in _outstandingMessages.GetMessagesExpiringBefore(time)) {
					if (!ActionTakenForRetriedMessage(message)) {
						RetryMessage(message.ResolvedEvent, message.RetryCount);
					}
				}

				TryPushingMessagesToClients();
				TryMarkCheckpoint(true);
				if ((_state & PersistentSubscriptionState.Behind |
				     PersistentSubscriptionState.OutstandingPageRequest) ==
				    PersistentSubscriptionState.Behind)
					TryReadingNewBatch();
			}
		}

		private bool ActionTakenForRetriedMessage(OutstandingMessage message) {
			if (message.RetryCount < _settings.MaxRetryCount) return false;
			ParkMessage(message.ResolvedEvent, string.Format("Reached retry count of {0}", _settings.MaxRetryCount), 0);
			return true;
		}

		private void RetryMessage(ResolvedEvent @event, int count) {
			Log.Debug("Retrying message {subscriptionId} {stream}/{eventNumber}", SubscriptionId,
				@event.OriginalStreamId, @event.OriginalEventNumber);
			_outstandingMessages.Remove(@event.OriginalEvent.EventId);
			_pushClients.RemoveProcessingMessage(@event.OriginalEvent.EventId);
			_streamBuffer.AddRetry(new OutstandingMessage(@event.OriginalEvent.EventId, null, @event, count + 1));
		}

		public MonitoringMessage.SubscriptionInfo GetStatistics() {
			return _statistics.GetStatistics();
		}

		public void RetrySingleMessage(ResolvedEvent @event) {
			_streamBuffer.AddRetry(new OutstandingMessage(@event.OriginalEvent.EventId, null, @event, 0));
		}

		public void Delete() {
			_settings.CheckpointWriter.BeginDelete(x => { });
			_settings.MessageParker.BeginDelete(x => { });
		}
	}
}
