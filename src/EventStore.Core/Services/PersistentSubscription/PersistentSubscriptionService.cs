using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Services.PersistentSubscription {
	public class PersistentSubscriptionService :
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<TcpMessage.ConnectionClosed>,
		IHandle<SystemMessage.BecomeMaster>,
		IHandle<SubscriptionMessage.PersistentSubscriptionTimerTick>,
		IHandle<ClientMessage.ReplayAllParkedMessages>,
		IHandle<ClientMessage.ReplayParkedMessage>,
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<ClientMessage.ConnectToPersistentSubscription>,
		IHandle<StorageMessage.EventCommitted>,
		IHandle<ClientMessage.UnsubscribeFromStream>,
		IHandle<ClientMessage.PersistentSubscriptionAckEvents>,
		IHandle<ClientMessage.PersistentSubscriptionNackEvents>,
		IHandle<ClientMessage.CreatePersistentSubscription>,
		IHandle<ClientMessage.UpdatePersistentSubscription>,
		IHandle<ClientMessage.DeletePersistentSubscription>,
		IHandle<ClientMessage.ReadNextNPersistentMessages>,
		IHandle<MonitoringMessage.GetAllPersistentSubscriptionStats>,
		IHandle<MonitoringMessage.GetPersistentSubscriptionStats>,
		IHandle<MonitoringMessage.GetStreamPersistentSubscriptionStats> {
		private static readonly ILogger Log = LogManager.GetLoggerFor<PersistentSubscriptionService>();

		private Dictionary<string, List<PersistentSubscription>> _subscriptionTopics;
		private Dictionary<string, PersistentSubscription> _subscriptionsById;

		private readonly IQueuedHandler _queuedHandler;
		private readonly IReadIndex _readIndex;
		private readonly IODispatcher _ioDispatcher;
		private readonly IPublisher _bus;
		private readonly PersistentSubscriptionConsumerStrategyRegistry _consumerStrategyRegistry;
		private readonly IPersistentSubscriptionCheckpointReader _checkpointReader;
		private readonly IPersistentSubscriptionStreamReader _streamReader;
		private PersistentSubscriptionConfig _config = new PersistentSubscriptionConfig();
		private bool _started = false;
		private VNodeState _state;
		private readonly TimerMessage.Schedule _tickRequestMessage;
		private bool _handleTick;

		internal PersistentSubscriptionService(IQueuedHandler queuedHandler, IReadIndex readIndex,
			IODispatcher ioDispatcher, IPublisher bus,
			PersistentSubscriptionConsumerStrategyRegistry consumerStrategyRegistry) {
			Ensure.NotNull(queuedHandler, "queuedHandler");
			Ensure.NotNull(readIndex, "readIndex");
			Ensure.NotNull(ioDispatcher, "ioDispatcher");

			_queuedHandler = queuedHandler;
			_readIndex = readIndex;
			_ioDispatcher = ioDispatcher;
			_bus = bus;
			_consumerStrategyRegistry = consumerStrategyRegistry;
			_checkpointReader = new PersistentSubscriptionCheckpointReader(_ioDispatcher);
			_streamReader = new PersistentSubscriptionStreamReader(_ioDispatcher, 100);
			//TODO CC configurable
			_tickRequestMessage = TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(1000),
				new PublishEnvelope(_bus),
				new SubscriptionMessage.PersistentSubscriptionTimerTick());
		}

		public void InitToEmpty() {
			_handleTick = false;
			_subscriptionTopics = new Dictionary<string, List<PersistentSubscription>>();
			_subscriptionsById = new Dictionary<string, PersistentSubscription>();
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			_state = message.State;

			if (message.State == VNodeState.Master) return;
			Log.Debug("Persistent subscriptions received state change to {state}. Stopping listening", _state);
			ShutdownSubscriptions();
			Stop();
		}

		public void Handle(SystemMessage.BecomeMaster message) {
			Log.Debug("Persistent subscriptions Became Master so now handling subscriptions");
			InitToEmpty();
			_handleTick = true;
			_bus.Publish(_tickRequestMessage);
			LoadConfiguration(Start);
		}


		public void Handle(SystemMessage.BecomeShuttingDown message) {
			ShutdownSubscriptions();
			Stop();
			_queuedHandler.RequestStop();
		}

		private void ShutdownSubscriptions() {
			if (_subscriptionsById == null) return;
			foreach (var subscription in _subscriptionsById.Values) {
				subscription.Shutdown();
			}
		}

		private void Start() {
			_started = true;
		}

		private void Stop() {
			_started = false;
		}

		public void Handle(ClientMessage.UnsubscribeFromStream message) {
			if (!_started) return;
			UnsubscribeFromStream(message.CorrelationId, true);
		}

		public void Handle(ClientMessage.CreatePersistentSubscription message) {
			if (!_started) return;
			var key = BuildSubscriptionGroupKey(message.EventStreamId, message.GroupName);
			Log.Debug("Creating persistent subscription {subscriptionKey}", key);
			//TODO revisit for permissions. maybe make admin only?
			var streamAccess =
				_readIndex.CheckStreamAccess(SystemStreams.SettingsStream, StreamAccessType.Write, message.User);

			if (!streamAccess.Granted) {
				message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionCompleted(
					message.CorrelationId,
					ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AccessDenied,
					"You do not have permissions to create streams"));
				return;
			}

			if (_subscriptionsById.ContainsKey(key)) {
				message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionCompleted(
					message.CorrelationId,
					ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult
						.AlreadyExists,
					"Group '" + message.GroupName + "' already exists."));
				return;
			}

			if (message.EventStreamId == null || message.EventStreamId == "" || message.EventStreamId == "$all") {
				message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionCompleted(
					message.CorrelationId,
					ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Fail,
					"Bad stream name."));
				return;
			}

			if (!_consumerStrategyRegistry.ValidateStrategy(message.NamedConsumerStrategy)) {
				message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionCompleted(
					message.CorrelationId,
					ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Fail,
					string.Format("Consumer strategy {0} does not exist.", message.NamedConsumerStrategy)));
				return;
			}

			CreateSubscriptionGroup(message.EventStreamId,
				message.GroupName,
				message.ResolveLinkTos,
				message.StartFrom,
				message.RecordStatistics,
				message.MaxRetryCount,
				message.LiveBufferSize,
				message.BufferSize,
				message.ReadBatchSize,
				ToTimeout(message.CheckPointAfterMilliseconds),
				message.MinCheckPointCount,
				message.MaxCheckPointCount,
				message.MaxSubscriberCount,
				message.NamedConsumerStrategy,
				ToTimeout(message.MessageTimeoutMilliseconds)
			);

			Log.Debug("New persistent subscription {subscriptionKey}", key);
			_config.Updated = DateTime.Now;
			_config.UpdatedBy = message.User.Identity.Name;
			_config.Entries.Add(new PersistentSubscriptionEntry {
				Stream = message.EventStreamId,
				Group = message.GroupName,
				ResolveLinkTos = message.ResolveLinkTos,
				CheckPointAfter = message.CheckPointAfterMilliseconds,
				ExtraStatistics = message.RecordStatistics,
				HistoryBufferSize = message.BufferSize,
				LiveBufferSize = message.LiveBufferSize,
				MaxCheckPointCount = message.MaxCheckPointCount,
				MinCheckPointCount = message.MinCheckPointCount,
				MaxRetryCount = message.MaxRetryCount,
				ReadBatchSize = message.ReadBatchSize,
				MaxSubscriberCount = message.MaxSubscriberCount,
				MessageTimeout = message.MessageTimeoutMilliseconds,
				NamedConsumerStrategy = message.NamedConsumerStrategy,
				StartFrom = message.StartFrom
			});
			SaveConfiguration(() => message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionCompleted(
				message.CorrelationId,
				ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Success, "")));
		}

		public void Handle(ClientMessage.UpdatePersistentSubscription message) {
			if (!_started) return;
			var key = BuildSubscriptionGroupKey(message.EventStreamId, message.GroupName);
			Log.Debug("Updating persistent subscription {subscriptionKey}", key);
			var streamAccess =
				_readIndex.CheckStreamAccess(SystemStreams.SettingsStream, StreamAccessType.Write, message.User);

			if (!streamAccess.Granted) {
				message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionCompleted(
					message.CorrelationId,
					ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.AccessDenied,
					"You do not have permissions to update the subscription"));
				return;
			}

			if (!_subscriptionsById.ContainsKey(key)) {
				message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionCompleted(
					message.CorrelationId,
					ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.DoesNotExist,
					"Group '" + message.GroupName + "' does not exist."));
				return;
			}

			if (!_consumerStrategyRegistry.ValidateStrategy(message.NamedConsumerStrategy)) {
				message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionCompleted(
					message.CorrelationId,
					ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.Fail,
					string.Format("Consumer strategy {0} does not exist.", message.NamedConsumerStrategy)));
				return;
			}

			RemoveSubscription(message.EventStreamId, message.GroupName);
			RemoveSubscriptionConfig(message.User.Identity.Name, message.EventStreamId, message.GroupName);
			CreateSubscriptionGroup(message.EventStreamId,
				message.GroupName,
				message.ResolveLinkTos,
				message.StartFrom,
				message.RecordStatistics,
				message.MaxRetryCount,
				message.LiveBufferSize,
				message.BufferSize,
				message.ReadBatchSize,
				ToTimeout(message.CheckPointAfterMilliseconds),
				message.MinCheckPointCount,
				message.MaxCheckPointCount,
				message.MaxSubscriberCount,
				message.NamedConsumerStrategy,
				ToTimeout(message.MessageTimeoutMilliseconds)
			);
			_config.Updated = DateTime.Now;
			_config.UpdatedBy = message.User.Identity.Name;
			_config.Entries.Add(new PersistentSubscriptionEntry() {
				Stream = message.EventStreamId,
				Group = message.GroupName,
				ResolveLinkTos = message.ResolveLinkTos,
				CheckPointAfter = message.CheckPointAfterMilliseconds,
				ExtraStatistics = message.RecordStatistics,
				HistoryBufferSize = message.BufferSize,
				LiveBufferSize = message.LiveBufferSize,
				MaxCheckPointCount = message.MaxCheckPointCount,
				MinCheckPointCount = message.MinCheckPointCount,
				MaxRetryCount = message.MaxRetryCount,
				ReadBatchSize = message.ReadBatchSize,
				MaxSubscriberCount = message.MaxSubscriberCount,
				MessageTimeout = message.MessageTimeoutMilliseconds,
				NamedConsumerStrategy = message.NamedConsumerStrategy,
				StartFrom = message.StartFrom
			});
			SaveConfiguration(() => message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionCompleted(
				message.CorrelationId,
				ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.Success, "")));
		}

		private void CreateSubscriptionGroup(string eventStreamId,
			string groupName,
			bool resolveLinkTos,
			long startFrom,
			bool extraStatistics,
			int maxRetryCount,
			int liveBufferSize,
			int historyBufferSize,
			int readBatchSize,
			TimeSpan checkPointAfter,
			int minCheckPointCount,
			int maxCheckPointCount,
			int maxSubscriberCount,
			string namedConsumerStrategy,
			TimeSpan messageTimeout) {
			var key = BuildSubscriptionGroupKey(eventStreamId, groupName);
			List<PersistentSubscription> subscribers;
			if (!_subscriptionTopics.TryGetValue(eventStreamId, out subscribers)) {
				subscribers = new List<PersistentSubscription>();
				_subscriptionTopics.Add(eventStreamId, subscribers);
			}

			var subscription = new PersistentSubscription(
				new PersistentSubscriptionParams(
					resolveLinkTos,
					key,
					eventStreamId,
					groupName,
					startFrom,
					extraStatistics,
					messageTimeout,
					maxRetryCount,
					liveBufferSize,
					historyBufferSize,
					readBatchSize,
					checkPointAfter,
					minCheckPointCount,
					maxCheckPointCount,
					maxSubscriberCount,
					_consumerStrategyRegistry.GetInstance(namedConsumerStrategy, key),
					_streamReader,
					_checkpointReader,
					new PersistentSubscriptionCheckpointWriter(key, _ioDispatcher),
					new PersistentSubscriptionMessageParker(key, _ioDispatcher)));
			_subscriptionsById[key] = subscription;
			subscribers.Add(subscription);
		}

		public void Handle(ClientMessage.DeletePersistentSubscription message) {
			if (!_started) return;
			var key = BuildSubscriptionGroupKey(message.EventStreamId, message.GroupName);
			Log.Debug("Deleting persistent subscription {subscriptionKey}", key);
			var streamAccess =
				_readIndex.CheckStreamAccess(SystemStreams.SettingsStream, StreamAccessType.Write, message.User);

			if (!streamAccess.Granted) {
				message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionCompleted(
					message.CorrelationId,
					ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.AccessDenied,
					"You do not have permissions to create streams"));
				return;
			}

			PersistentSubscription subscription;
			if (!_subscriptionsById.TryGetValue(key, out subscription)) {
				message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionCompleted(
					message.CorrelationId,
					ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.DoesNotExist,
					"Group '" + message.GroupName + "' does not exist."));
				return;
			}

			if (!_subscriptionTopics.ContainsKey(message.EventStreamId)) {
				message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionCompleted(
					message.CorrelationId,
					ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.Fail,
					"Group '" + message.GroupName + "' does not exist."));
				return;
			}

			RemoveSubscription(message.EventStreamId, message.GroupName);
			RemoveSubscriptionConfig(message.User.Identity.Name, message.EventStreamId, message.GroupName);
			subscription.Delete();
			SaveConfiguration(() => message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionCompleted(
				message.CorrelationId,
				ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.Success, "")));
		}

		private void RemoveSubscriptionConfig(string username, string eventStreamId, string groupName) {
			_config.Updated = DateTime.Now;
			_config.UpdatedBy = username;
			var index = _config.Entries.FindLastIndex(x => x.Stream == eventStreamId && x.Group == groupName);
			_config.Entries.RemoveAt(index);
		}

		private void RemoveSubscription(string eventStreamId, string groupName) {
			List<PersistentSubscription> subscribers;
			var key = BuildSubscriptionGroupKey(eventStreamId, groupName);
			_subscriptionsById.Remove(key);
			if (_subscriptionTopics.TryGetValue(eventStreamId, out subscribers)) {
				for (int i = 0; i < subscribers.Count; i++) {
					var sub = subscribers[i];
					if (sub.SubscriptionId == key) {
						sub.Shutdown();
						subscribers.RemoveAt(i);
						break;
					}
				}
			}
		}

		private void UnsubscribeFromStream(Guid correlationId, bool sendDropNotification) {
			foreach (var subscription in _subscriptionsById.Values) {
				subscription.RemoveClientByCorrelationId(correlationId, sendDropNotification);
			}
		}

		public void Handle(TcpMessage.ConnectionClosed message) {
			//TODO CC make a map for this
			Log.Debug("Persistent subscription lost connection from {remoteEndPoint}",
				message.Connection.RemoteEndPoint);
			if (_subscriptionsById == null) return; //havn't built yet.
			foreach (var subscription in _subscriptionsById.Values) {
				subscription.RemoveClientByConnectionId(message.Connection.ConnectionId);
			}
		}

		public void Handle(ClientMessage.ConnectToPersistentSubscription message) {
			if (!_started) return;
			var streamAccess = _readIndex.CheckStreamAccess(
				message.EventStreamId, StreamAccessType.Read, message.User);

			if (!streamAccess.Granted) {
				message.Envelope.ReplyWith(new ClientMessage.SubscriptionDropped(message.CorrelationId,
					SubscriptionDropReason.AccessDenied));
				return;
			}

			List<PersistentSubscription> subscribers;
			if (!_subscriptionTopics.TryGetValue(message.EventStreamId, out subscribers)) {
				message.Envelope.ReplyWith(new ClientMessage.SubscriptionDropped(message.CorrelationId,
					SubscriptionDropReason.NotFound));
				return;
			}

			var key = BuildSubscriptionGroupKey(message.EventStreamId, message.SubscriptionId);
			PersistentSubscription subscription;
			if (!_subscriptionsById.TryGetValue(key, out subscription)) {
				message.Envelope.ReplyWith(new ClientMessage.SubscriptionDropped(message.CorrelationId,
					SubscriptionDropReason.NotFound));
				return;
			}

			if (subscription.HasReachedMaxClientCount) {
				message.Envelope.ReplyWith(new ClientMessage.SubscriptionDropped(message.CorrelationId,
					SubscriptionDropReason.SubscriberMaxCountReached));
				return;
			}

			Log.Debug("New connection to persistent subscription {subscriptionKey} by {connectionId}", key,
				message.ConnectionId);
			var lastEventNumber = _readIndex.GetStreamLastEventNumber(message.EventStreamId);
			var lastCommitPos = _readIndex.LastCommitPosition;
			var subscribedMessage =
				new ClientMessage.PersistentSubscriptionConfirmation(key, message.CorrelationId, lastCommitPos,
					lastEventNumber);
			message.Envelope.ReplyWith(subscribedMessage);
			var name = message.User == null ? "anonymous" : message.User.Identity.Name;
			subscription.AddClient(message.CorrelationId, message.ConnectionId, message.Envelope,
				message.AllowedInFlightMessages, name, message.From);
		}

		private static string BuildSubscriptionGroupKey(string stream, string groupName) {
			return stream + "::" + groupName;
		}

		public void Handle(StorageMessage.EventCommitted message) {
			if (!_started) return;
			ProcessEventCommited(message.Event.EventStreamId, message.CommitPosition, message.Event);
		}

		private void ProcessEventCommited(string eventStreamId, long commitPosition, EventRecord evnt) {
			List<PersistentSubscription> subscriptions;
			if (!_subscriptionTopics.TryGetValue(eventStreamId, out subscriptions))
				return;
			for (int i = 0, n = subscriptions.Count; i < n; i++) {
				var subscr = subscriptions[i];
				var pair = ResolvedEvent.ForUnresolvedEvent(evnt, commitPosition);
				if (subscr.ResolveLinkTos)
					pair = ResolveLinkToEvent(evnt, commitPosition); //TODO this can be cached
				subscr.NotifyLiveSubscriptionMessage(pair);
			}
		}

		private ResolvedEvent ResolveLinkToEvent(EventRecord eventRecord, long commitPosition) {
			if (eventRecord.EventType == SystemEventTypes.LinkTo) {
				try {
					string[] parts = Helper.UTF8NoBom.GetString(eventRecord.Data).Split('@');
					long eventNumber = long.Parse(parts[0]);
					string streamId = parts[1];

					var res = _readIndex.ReadEvent(streamId, eventNumber);
					if (res.Result == ReadEventResult.Success)
						return ResolvedEvent.ForResolvedLink(res.Record, eventRecord, commitPosition);

					return ResolvedEvent.ForFailedResolvedLink(eventRecord, res.Result, commitPosition);
				} catch (Exception exc) {
					Log.ErrorException(exc, "Error while resolving link for event record: {eventRecord}",
						eventRecord.ToString());
				}

				return ResolvedEvent.ForFailedResolvedLink(eventRecord, ReadEventResult.Error, commitPosition);
			}

			return ResolvedEvent.ForUnresolvedEvent(eventRecord, commitPosition);
		}

		public void Handle(ClientMessage.PersistentSubscriptionAckEvents message) {
			if (!_started) return;
			PersistentSubscription subscription;
			if (_subscriptionsById.TryGetValue(message.SubscriptionId, out subscription)) {
				subscription.AcknowledgeMessagesProcessed(message.CorrelationId, message.ProcessedEventIds);
			}
		}

		public void Handle(ClientMessage.PersistentSubscriptionNackEvents message) {
			if (!_started) return;
			PersistentSubscription subscription;
			if (_subscriptionsById.TryGetValue(message.SubscriptionId, out subscription)) {
				subscription.NotAcknowledgeMessagesProcessed(message.CorrelationId, message.ProcessedEventIds,
					(NakAction)message.Action, message.Message);
			}
		}

		public void Handle(ClientMessage.ReadNextNPersistentMessages message) {
			if (!_started) return;
			var streamAccess = _readIndex.CheckStreamAccess(
				message.EventStreamId, StreamAccessType.Read, message.User);

			if (!streamAccess.Granted) {
				message.Envelope.ReplyWith(
					new ClientMessage.ReadNextNPersistentMessagesCompleted(message.CorrelationId,
						ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult
							.AccessDenied,
						"Access Denied.",
						null));
				return;
			}

			List<PersistentSubscription> subscribers;
			if (!_subscriptionTopics.TryGetValue(message.EventStreamId, out subscribers)) {
				message.Envelope.ReplyWith(
					new ClientMessage.ReadNextNPersistentMessagesCompleted(message.CorrelationId,
						ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult
							.DoesNotExist,
						"Not found.",
						null));
				return;
			}

			var key = BuildSubscriptionGroupKey(message.EventStreamId, message.GroupName);
			PersistentSubscription subscription;
			if (!_subscriptionsById.TryGetValue(key, out subscription)) {
				message.Envelope.ReplyWith(
					new ClientMessage.ReadNextNPersistentMessagesCompleted(message.CorrelationId,
						ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult
							.DoesNotExist,
						"Not found.",
						null));
				return;
			}

			var messages = subscription.GetNextNOrLessMessages(message.Count).ToArray();
			message.Envelope.ReplyWith(
				new ClientMessage.ReadNextNPersistentMessagesCompleted(message.CorrelationId,
					ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult.Success,
					string.Format("{0} read.", messages.Length),
					messages));
		}

		public void Handle(ClientMessage.ReplayAllParkedMessages message) {
			PersistentSubscription subscription;
			var key = BuildSubscriptionGroupKey(message.EventStreamId, message.GroupName);
			Log.Debug("Replaying parked messages for persistent subscription {subscriptionKey}", key);
			var streamAccess =
				_readIndex.CheckStreamAccess(SystemStreams.SettingsStream, StreamAccessType.Write, message.User);

			if (!streamAccess.Granted) {
				message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
					ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.AccessDenied,
					"You do not have permissions to replay messages"));
				return;
			}

			if (!_subscriptionsById.TryGetValue(key, out subscription)) {
				message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
					ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.DoesNotExist,
					"Unable to locate '" + key + "'"));
				return;
			}

			subscription.RetryAllParkedMessages();
			message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
				ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.Success, ""));
		}

		public void Handle(ClientMessage.ReplayParkedMessage message) {
			var key = BuildSubscriptionGroupKey(message.EventStreamId, message.GroupName);
			PersistentSubscription subscription;
			var streamAccess =
				_readIndex.CheckStreamAccess(SystemStreams.SettingsStream, StreamAccessType.Write, message.User);

			if (!streamAccess.Granted) {
				message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
					ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.AccessDenied,
					"You do not have permissions to replay messages"));
				return;
			}

			if (!_subscriptionsById.TryGetValue(key, out subscription)) {
				message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
					ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.DoesNotExist,
					"Unable to locate '" + key + "'"));
				return;
			}

			subscription.RetrySingleMessage(message.Event);
			message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
				ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.Success, ""));
		}

		private void LoadConfiguration(Action continueWith) {
			_ioDispatcher.ReadBackward(SystemStreams.PersistentSubscriptionConfig, -1, 1, false,
				SystemAccount.Principal, x => HandleLoadCompleted(continueWith, x));
		}

		private void HandleLoadCompleted(Action continueWith,
			ClientMessage.ReadStreamEventsBackwardCompleted readStreamEventsBackwardCompleted) {
			switch (readStreamEventsBackwardCompleted.Result) {
				case ReadStreamResult.Success:
					try {
						_config =
							PersistentSubscriptionConfig.FromSerializedForm(
								readStreamEventsBackwardCompleted.Events[0].Event.Data);
						foreach (var entry in _config.Entries) {
							if (!_consumerStrategyRegistry.ValidateStrategy(entry.NamedConsumerStrategy)) {
								Log.Error(
									"A persistent subscription exists with an invalid consumer strategy '{strategy}'. Ignoring it.",
									entry.NamedConsumerStrategy);
								continue;
							}

							CreateSubscriptionGroup(entry.Stream,
								entry.Group,
								entry.ResolveLinkTos,
								entry.StartFrom,
								entry.ExtraStatistics,
								entry.MaxRetryCount,
								entry.LiveBufferSize,
								entry.HistoryBufferSize,
								entry.ReadBatchSize,
								ToTimeout(entry.CheckPointAfter),
								entry.MinCheckPointCount,
								entry.MaxCheckPointCount,
								entry.MaxSubscriberCount,
								entry.NamedConsumerStrategy,
								ToTimeout(entry.MessageTimeout));
						}

						continueWith();
					} catch (Exception ex) {
						Log.ErrorException(ex, "There was an error loading configuration from storage.");
					}

					break;
				case ReadStreamResult.NoStream:
					_config = new PersistentSubscriptionConfig {Version = "2"};
					continueWith();
					break;
				default:
					throw new Exception(readStreamEventsBackwardCompleted.Result +
					                    " is an unexpected result writing subscription configuration.");
			}
		}

		private void SaveConfiguration(Action continueWith) {
			Log.Debug("Saving persistent subscription configuration");
			var data = _config.GetSerializedForm();
			var ev = new Event(Guid.NewGuid(), "PersistentConfig1", true, data, new byte[0]);
			var metadata = new StreamMetadata(maxCount: 2);
			Lazy<StreamMetadata> streamMetadata = new Lazy<StreamMetadata>(() => metadata);
			Event[] events = new Event[] {ev};
			_ioDispatcher.ConfigureStreamAndWriteEvents(SystemStreams.PersistentSubscriptionConfig,
				ExpectedVersion.Any, streamMetadata, events, SystemAccount.Principal,
				x => HandleSaveConfigurationCompleted(continueWith, x));
		}

		private void HandleSaveConfigurationCompleted(Action continueWith, ClientMessage.WriteEventsCompleted obj) {
			switch (obj.Result) {
				case OperationResult.Success:
					continueWith();
					break;
				case OperationResult.CommitTimeout:
				case OperationResult.PrepareTimeout:
					Log.Info("Timeout while trying to save persistent subscription configuration. Retrying");
					SaveConfiguration(continueWith);
					break;
				default:
					throw new Exception(obj.Result +
					                    " is an unexpected result writing persistent subscription configuration.");
			}
		}

		public void Handle(MonitoringMessage.GetPersistentSubscriptionStats message) {
			if (!_started) {
				message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
					MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotReady, null)
				);
				return;
			}

			List<PersistentSubscription> subscribers;
			if (!_subscriptionTopics.TryGetValue(message.EventStreamId, out subscribers) || subscribers == null) {
				message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
					MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotFound, null)
				);
				return;
			}

			var subscription = subscribers.FirstOrDefault(x => x.GroupName == message.GroupName);
			if (subscription == null) {
				message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
					MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotFound, null)
				);
				return;
			}

			var stats = new List<MonitoringMessage.SubscriptionInfo>() {
				subscription.GetStatistics()
			};
			message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
				MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.Success, stats)
			);
		}

		public void Handle(MonitoringMessage.GetStreamPersistentSubscriptionStats message) {
			if (!_started) {
				message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
					MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotReady, null)
				);
				return;
			}

			List<PersistentSubscription> subscribers;
			if (!_subscriptionTopics.TryGetValue(message.EventStreamId, out subscribers)) {
				message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
					MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotFound, null)
				);
				return;
			}

			var stats = subscribers.Select(sub => sub.GetStatistics()).ToList();
			message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
				MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.Success, stats)
			);
		}

		public void Handle(MonitoringMessage.GetAllPersistentSubscriptionStats message) {
			if (!_started) {
				message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
					MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotReady, null)
				);
				return;
			}

			var stats = (from subscription in _subscriptionTopics.Values
				from sub in subscription
				select sub.GetStatistics()).ToList();
			message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
				MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.Success, stats)
			);
		}

		public void Handle(SubscriptionMessage.PersistentSubscriptionTimerTick message) {
			if (!_handleTick) return;
			try {
				WakeSubscriptions();
			} finally {
				_bus.Publish(_tickRequestMessage);
			}
		}

		private void WakeSubscriptions() {
			var now = DateTime.UtcNow;

			foreach (var subscription in _subscriptionsById.Values) {
				subscription.NotifyClockTick(now);
			}
		}

		private TimeSpan ToTimeout(int milliseconds) {
			return milliseconds == 0 ? TimeSpan.MaxValue : TimeSpan.FromMilliseconds(milliseconds);
		}
	}
}
