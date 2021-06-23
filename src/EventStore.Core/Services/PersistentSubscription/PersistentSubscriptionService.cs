using System;
using System.Collections.Generic;
using System.Linq;
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
using ILogger = Serilog.ILogger;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Services.PersistentSubscription {
	public abstract class PersistentSubscriptionService {
		protected static readonly ILogger Log = Serilog.Log.ForContext<PersistentSubscriptionService>();
	}

	public class PersistentSubscriptionService<TStreamId> :
		PersistentSubscriptionService,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<TcpMessage.ConnectionClosed>,
		IHandle<SystemMessage.BecomeLeader>,
		IHandle<SubscriptionMessage.PersistentSubscriptionsRestart>,
		IHandle<SubscriptionMessage.PersistentSubscriptionTimerTick>,
		IHandle<ClientMessage.ReplayParkedMessages>,
		IHandle<ClientMessage.ReplayParkedMessage>,
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<ClientMessage.ConnectToPersistentSubscriptionToStream>,
		IHandle<ClientMessage.ConnectToPersistentSubscriptionToAll>,
		IHandle<StorageMessage.EventCommitted>,
		IHandle<ClientMessage.UnsubscribeFromStream>,
		IHandle<ClientMessage.PersistentSubscriptionAckEvents>,
		IHandle<ClientMessage.PersistentSubscriptionNackEvents>,
		IHandle<ClientMessage.CreatePersistentSubscriptionToStream>,
		IHandle<ClientMessage.UpdatePersistentSubscriptionToStream>,
		IHandle<ClientMessage.DeletePersistentSubscriptionToStream>,
		IHandle<ClientMessage.CreatePersistentSubscriptionToAll>,
		IHandle<ClientMessage.UpdatePersistentSubscriptionToAll>,
		IHandle<ClientMessage.DeletePersistentSubscriptionToAll>,
		IHandle<ClientMessage.ReadNextNPersistentMessages>,
		IHandle<MonitoringMessage.GetAllPersistentSubscriptionStats>,
		IHandle<MonitoringMessage.GetPersistentSubscriptionStats>,
		IHandle<MonitoringMessage.GetStreamPersistentSubscriptionStats> {

		private Dictionary<string, List<PersistentSubscription>> _subscriptionTopics;
		private Dictionary<string, PersistentSubscription> _subscriptionsById;

		private readonly IQueuedHandler _queuedHandler;
		private readonly IReadIndex<TStreamId> _readIndex;
		private readonly IODispatcher _ioDispatcher;
		private readonly IPublisher _bus;
		private readonly PersistentSubscriptionConsumerStrategyRegistry _consumerStrategyRegistry;
		private readonly IPersistentSubscriptionCheckpointReader _checkpointReader;
		private readonly IPersistentSubscriptionStreamReader _streamReader;
		private PersistentSubscriptionConfig _config = new PersistentSubscriptionConfig();
		private bool _started = false;
		private VNodeState _state;
		private Guid _timerTickCorrelationId;
		private bool _handleTick;

		internal PersistentSubscriptionService(IQueuedHandler queuedHandler, IReadIndex<TStreamId> readIndex,
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
			_timerTickCorrelationId = Guid.NewGuid();
		}

		public void InitToEmpty() {
			_handleTick = false;
			_subscriptionTopics = new Dictionary<string, List<PersistentSubscription>>();
			_subscriptionsById = new Dictionary<string, PersistentSubscription>();
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			_state = message.State;

			if (message.State == VNodeState.Leader) return;
			Log.Debug("Persistent subscriptions received state change to {state}. Stopping listening", _state);
			ShutdownSubscriptions();
			Stop();
		}

		public void Handle(SystemMessage.BecomeLeader message) {
			Log.Debug("Persistent subscriptions Became Leader so now handling subscriptions");
			StartSubscriptions();
		}
		
		public void Handle(SubscriptionMessage.PersistentSubscriptionsRestart message) {
			if (!_started) {
				message.ReplyEnvelope.ReplyWith(new SubscriptionMessage.InvalidPersistentSubscriptionsRestart());
				return;
			}
			
			Log.Debug("Persistent Subscriptions are being restarted");
			message.ReplyEnvelope.ReplyWith(new SubscriptionMessage.PersistentSubscriptionsRestarting());
			
			Stop();
			ShutdownSubscriptions();
			StartSubscriptions();
		}

		private void StartSubscriptions() {
			InitToEmpty();
			_handleTick = true;
			_timerTickCorrelationId = Guid.NewGuid();
			_bus.Publish(TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(1000),
				new PublishEnvelope(_bus),
				new SubscriptionMessage.PersistentSubscriptionTimerTick(_timerTickCorrelationId)));
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
			_bus.Publish(new SubscriptionMessage.PersistentSubscriptionsStarted());
		}

		private void Stop() {
			_started = false;
			_bus.Publish(new SubscriptionMessage.PersistentSubscriptionsStopped());
		}

		public void Handle(ClientMessage.UnsubscribeFromStream message) {
			if (!_started) return;
			UnsubscribeFromStream(message.CorrelationId, true);
		}

		private bool ValidateStartFrom(IPersistentSubscriptionStreamPosition startFromPosition, out string error) {
			switch (startFromPosition)
			{
				case PersistentSubscriptionSingleStreamPosition startFromStream:
				{
					if (startFromStream.StreamEventNumber < -1) {
						error = "Invalid Start From position: event number must be greater than or equal to -1.";
						return false;
					}

					error = null;
					return true;
				}
				case PersistentSubscriptionAllStreamPosition startFromAll:
				{
					var (commit, prepare) = startFromAll.TFPosition;

					if (prepare > commit) {
						error = "Invalid Start From position: prepare position must be less than or equal to the commit position.";
						return false;
					}

					if (commit > _readIndex.LastIndexedPosition) {
						error =
							"Invalid Start From position: commit position must be less than or equal to the last commit position in the transaction file.";
						return false;
					}

					if ((prepare <= -1 || commit <= -1) && !(prepare == -1 && commit == -1)) {
						error =
							"Invalid Start From position: prepare and commit positions must be greater than or equal to 0 or both equal to -1.";
						return false;
					}

					error = null;
					return true;
				}
				default:
					throw new InvalidOperationException();
			}
		}

		private void CreatePersistentSubscription(
				IPersistentSubscriptionEventSource eventSource,
				string groupName,
				IPersistentSubscriptionStreamPosition startFrom,
				int messageTimeoutMilliseconds,
				bool resolveLinkTos,
				int maxRetryCount,
				int bufferSize,
				int liveBufferSize,
				int readBatchSize,
				int maxSubscriberCount,
				string namedConsumerStrategy,
				int maxCheckPointCount,
				int minCheckPointCount,
				int checkPointAfterMilliseconds,
				bool recordStatistics,
				Action<string> onSuccess,
				Action<string> onFail,
				Action<string> onExists,
				Action<string> onAccessDenied,
				string user
		) {
			if (!_started) return;
			var key = BuildSubscriptionGroupKey(eventSource.ToString(), groupName);
			Log.Debug("Creating persistent subscription {subscriptionKey}", key);

			if (_subscriptionsById.ContainsKey(key)) {
				onExists($"Group '{groupName}' already exists.");
				return;
			}

			if (!_consumerStrategyRegistry.ValidateStrategy(namedConsumerStrategy)) {
				onFail($"Consumer strategy {namedConsumerStrategy} does not exist.");
				return;
			}

			if (!ValidateStartFrom(startFrom, out var startFromValidationError)) {
				onFail(startFromValidationError);
				return;
			}

			CreateSubscriptionGroup(eventSource,
				groupName,
				resolveLinkTos,
				startFrom,
				recordStatistics,
				maxRetryCount,
				liveBufferSize,
				bufferSize,
				readBatchSize,
				ToCheckPointAfterTimeout(checkPointAfterMilliseconds),
				minCheckPointCount,
				maxCheckPointCount,
				maxSubscriberCount,
				namedConsumerStrategy,
				ToMessageTimeout(messageTimeoutMilliseconds)
			);

			Log.Debug("New persistent subscription {subscriptionKey}", key);
			_config.Updated = DateTime.Now;
			_config.UpdatedBy = user;
			_config.Entries.Add(new PersistentSubscriptionEntry {
				Stream = eventSource.ToString(), //'Stream' name kept for backward compatibility
				Filter = EventFilter.ParseToDto(eventSource.EventFilter),
				Group = groupName,
				ResolveLinkTos = resolveLinkTos,
				CheckPointAfter = checkPointAfterMilliseconds,
				ExtraStatistics = recordStatistics,
				HistoryBufferSize = bufferSize,
				LiveBufferSize = liveBufferSize,
				MaxCheckPointCount = maxCheckPointCount,
				MinCheckPointCount = minCheckPointCount,
				MaxRetryCount = maxRetryCount,
				ReadBatchSize = readBatchSize,
				MaxSubscriberCount = maxSubscriberCount,
				MessageTimeout = messageTimeoutMilliseconds,
				NamedConsumerStrategy = namedConsumerStrategy,
				#pragma warning disable 612
				StartFrom = startFrom is PersistentSubscriptionSingleStreamPosition x ? x.StreamEventNumber : long.MinValue,
				#pragma warning restore 612
				StartPosition = startFrom.ToString()
			});
			SaveConfiguration(() => onSuccess(""));
		}

		public void Handle(ClientMessage.CreatePersistentSubscriptionToStream message) {
			if (string.IsNullOrEmpty(message.EventStreamId) || message.EventStreamId == SystemStreams.AllStream) {
				message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionToStreamCompleted(
					message.CorrelationId,
					ClientMessage.CreatePersistentSubscriptionToStreamCompleted.CreatePersistentSubscriptionToStreamResult.Fail,
					"Bad stream name."));
				return;
			}

			CreatePersistentSubscription(
				new PersistentSubscriptionSingleStreamEventSource(message.EventStreamId),
				message.GroupName,
				new PersistentSubscriptionSingleStreamPosition(message.StartFrom),
				message.MessageTimeoutMilliseconds,
				message.ResolveLinkTos,
				message.MaxRetryCount,
				message.BufferSize,
				message.LiveBufferSize,
				message.ReadBatchSize,
				message.MaxSubscriberCount,
				message.NamedConsumerStrategy,
				message.MaxCheckPointCount,
				message.MinCheckPointCount,
				message.CheckPointAfterMilliseconds,
				message.RecordStatistics,
				(msg) => {
					message.Envelope.ReplyWith(
						new ClientMessage.CreatePersistentSubscriptionToStreamCompleted(
							message.CorrelationId,
							ClientMessage.CreatePersistentSubscriptionToStreamCompleted.CreatePersistentSubscriptionToStreamResult.Success,
							msg));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionToStreamCompleted(
						message.CorrelationId,
						ClientMessage.CreatePersistentSubscriptionToStreamCompleted.CreatePersistentSubscriptionToStreamResult.Fail,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionToStreamCompleted(
						message.CorrelationId,
						ClientMessage.CreatePersistentSubscriptionToStreamCompleted.CreatePersistentSubscriptionToStreamResult.AlreadyExists,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionToStreamCompleted(
						message.CorrelationId,
						ClientMessage.CreatePersistentSubscriptionToStreamCompleted.CreatePersistentSubscriptionToStreamResult.AccessDenied,
						error));
				},
				message.User?.Identity?.Name
				);
		}

		public void Handle(ClientMessage.CreatePersistentSubscriptionToAll message) {
			CreatePersistentSubscription(
				new PersistentSubscriptionAllStreamEventSource(message.EventFilter),
				message.GroupName,
				new PersistentSubscriptionAllStreamPosition(message.StartFrom.CommitPosition, message.StartFrom.PreparePosition),
				message.MessageTimeoutMilliseconds,
				message.ResolveLinkTos,
				message.MaxRetryCount,
				message.BufferSize,
				message.LiveBufferSize,
				message.ReadBatchSize,
				message.MaxSubscriberCount,
				message.NamedConsumerStrategy,
				message.MaxCheckPointCount,
				message.MinCheckPointCount,
				message.CheckPointAfterMilliseconds,
				message.RecordStatistics,
				(msg) => {
					message.Envelope.ReplyWith(
						new ClientMessage.CreatePersistentSubscriptionToAllCompleted(
							message.CorrelationId,
							ClientMessage.CreatePersistentSubscriptionToAllCompleted.CreatePersistentSubscriptionToAllResult.Success,
							msg));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionToAllCompleted(
						message.CorrelationId,
						ClientMessage.CreatePersistentSubscriptionToAllCompleted.CreatePersistentSubscriptionToAllResult.Fail,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionToAllCompleted(
						message.CorrelationId,
						ClientMessage.CreatePersistentSubscriptionToAllCompleted.CreatePersistentSubscriptionToAllResult.AlreadyExists,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionToAllCompleted(
						message.CorrelationId,
						ClientMessage.CreatePersistentSubscriptionToAllCompleted.CreatePersistentSubscriptionToAllResult.AccessDenied,
						error));
				},
				message.User?.Identity?.Name
			);
		}

		private void UpdatePersistentSubscription(
				IPersistentSubscriptionEventSource eventSource,
				string groupName,
				IPersistentSubscriptionStreamPosition startFrom,
				int messageTimeoutMilliseconds,
				bool resolveLinkTos,
				int maxRetryCount,
				int bufferSize,
				int liveBufferSize,
				int readBatchSize,
				int maxSubscriberCount,
				string namedConsumerStrategy,
				int maxCheckPointCount,
				int minCheckPointCount,
				int checkPointAfterMilliseconds,
				bool recordStatistics,
				Action<string> onSuccess,
				Action<string> onFail,
				Action<string> onNotExist,
				Action<string> onAccessDenied,
				string user
		) {
			if (!_started) return;
			var key = BuildSubscriptionGroupKey(eventSource.ToString(), groupName);
			Log.Debug("Updating persistent subscription {subscriptionKey}", key);

			if (!_subscriptionsById.ContainsKey(key)) {
				onNotExist($"Group '{groupName}' does not exist.");
				return;
			}

			if (!_consumerStrategyRegistry.ValidateStrategy(namedConsumerStrategy)) {
				onFail($"Consumer strategy {namedConsumerStrategy} does not exist.");
				return;
			}

			if (!ValidateStartFrom(startFrom, out var startFromValidationError)) {
				onFail(startFromValidationError);
				return;
			}

			RemoveSubscription(eventSource.ToString(), groupName);
			RemoveSubscriptionConfig(user, eventSource.ToString(), groupName);

			CreateSubscriptionGroup(eventSource,
				groupName,
				resolveLinkTos,
				startFrom,
				recordStatistics,
				maxRetryCount,
				liveBufferSize,
				bufferSize,
				readBatchSize,
				ToCheckPointAfterTimeout(checkPointAfterMilliseconds),
				minCheckPointCount,
				maxCheckPointCount,
				maxSubscriberCount,
				namedConsumerStrategy,
				ToMessageTimeout(messageTimeoutMilliseconds)
			);

			_config.Updated = DateTime.Now;
			_config.UpdatedBy = user;
			_config.Entries.Add(new PersistentSubscriptionEntry {
				Stream = eventSource.ToString(), //'Stream' name kept for backward compatibility
				Group = groupName,
				Filter = EventFilter.ParseToDto(eventSource.EventFilter),
				ResolveLinkTos = resolveLinkTos,
				CheckPointAfter = checkPointAfterMilliseconds,
				ExtraStatistics = recordStatistics,
				HistoryBufferSize = bufferSize,
				LiveBufferSize = liveBufferSize,
				MaxCheckPointCount = maxCheckPointCount,
				MinCheckPointCount = minCheckPointCount,
				MaxRetryCount = maxRetryCount,
				ReadBatchSize = readBatchSize,
				MaxSubscriberCount = maxSubscriberCount,
				MessageTimeout = messageTimeoutMilliseconds,
				NamedConsumerStrategy = namedConsumerStrategy,
				#pragma warning disable 612
				StartFrom = startFrom is PersistentSubscriptionSingleStreamPosition x ? x.StreamEventNumber : long.MinValue,
				#pragma warning restore 612
				StartPosition = startFrom.ToString()
			});
			SaveConfiguration(() => onSuccess(""));
		}

		public void Handle(ClientMessage.UpdatePersistentSubscriptionToStream message) {
			if (string.IsNullOrEmpty(message.EventStreamId) || message.EventStreamId == SystemStreams.AllStream) {
				message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionToStreamCompleted(
					message.CorrelationId,
					ClientMessage.UpdatePersistentSubscriptionToStreamCompleted.UpdatePersistentSubscriptionToStreamResult.Fail,
					"Bad stream name."));
				return;
			}

			UpdatePersistentSubscription(
				new PersistentSubscriptionSingleStreamEventSource(message.EventStreamId),
				message.GroupName,
				new PersistentSubscriptionSingleStreamPosition(message.StartFrom),
				message.MessageTimeoutMilliseconds,
				message.ResolveLinkTos,
				message.MaxRetryCount,
				message.BufferSize,
				message.LiveBufferSize,
				message.ReadBatchSize,
				message.MaxSubscriberCount,
				message.NamedConsumerStrategy,
				message.MaxCheckPointCount,
				message.MinCheckPointCount,
				message.CheckPointAfterMilliseconds,
				message.RecordStatistics,
				(msg) => {
					message.Envelope.ReplyWith(
						new ClientMessage.UpdatePersistentSubscriptionToStreamCompleted(
							message.CorrelationId,
							ClientMessage.UpdatePersistentSubscriptionToStreamCompleted.UpdatePersistentSubscriptionToStreamResult.Success,
							msg));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionToStreamCompleted(
						message.CorrelationId,
						ClientMessage.UpdatePersistentSubscriptionToStreamCompleted.UpdatePersistentSubscriptionToStreamResult.Fail,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionToStreamCompleted(
						message.CorrelationId,
						ClientMessage.UpdatePersistentSubscriptionToStreamCompleted.UpdatePersistentSubscriptionToStreamResult.DoesNotExist,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionToStreamCompleted(
						message.CorrelationId,
						ClientMessage.UpdatePersistentSubscriptionToStreamCompleted.UpdatePersistentSubscriptionToStreamResult.AccessDenied,
						error));
				},
				message.User?.Identity?.Name
			);
		}

		public void Handle(ClientMessage.UpdatePersistentSubscriptionToAll message) {
			UpdatePersistentSubscription(
				new PersistentSubscriptionAllStreamEventSource(),
				message.GroupName,
				new PersistentSubscriptionAllStreamPosition(message.StartFrom.CommitPosition, message.StartFrom.PreparePosition),
				message.MessageTimeoutMilliseconds,
				message.ResolveLinkTos,
				message.MaxRetryCount,
				message.BufferSize,
				message.LiveBufferSize,
				message.ReadBatchSize,
				message.MaxSubscriberCount,
				message.NamedConsumerStrategy,
				message.MaxCheckPointCount,
				message.MinCheckPointCount,
				message.CheckPointAfterMilliseconds,
				message.RecordStatistics,
				(msg) => {
					message.Envelope.ReplyWith(
						new ClientMessage.UpdatePersistentSubscriptionToAllCompleted(
							message.CorrelationId,
							ClientMessage.UpdatePersistentSubscriptionToAllCompleted.UpdatePersistentSubscriptionToAllResult.Success,
							msg));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionToAllCompleted(
						message.CorrelationId,
						ClientMessage.UpdatePersistentSubscriptionToAllCompleted.UpdatePersistentSubscriptionToAllResult.Fail,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionToAllCompleted(
						message.CorrelationId,
						ClientMessage.UpdatePersistentSubscriptionToAllCompleted.UpdatePersistentSubscriptionToAllResult.DoesNotExist,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.UpdatePersistentSubscriptionToAllCompleted(
						message.CorrelationId,
						ClientMessage.UpdatePersistentSubscriptionToAllCompleted.UpdatePersistentSubscriptionToAllResult.AccessDenied,
						error));
				},
				message.User?.Identity?.Name
			);
		}


		private void CreateSubscriptionGroup(IPersistentSubscriptionEventSource eventSource,
			string groupName,
			bool resolveLinkTos,
			IPersistentSubscriptionStreamPosition startFrom,
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
			var key = BuildSubscriptionGroupKey(eventSource.ToString(), groupName);
			List<PersistentSubscription> subscribers;
			if (!_subscriptionTopics.TryGetValue(eventSource.ToString(), out subscribers)) {
				subscribers = new List<PersistentSubscription>();
				_subscriptionTopics.Add(eventSource.ToString(), subscribers);
			}

			var subscription = new PersistentSubscription(
				new PersistentSubscriptionParams(
					resolveLinkTos,
					key,
					eventSource,
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

		private void DeletePersistentSubscription(
				IPersistentSubscriptionEventSource eventSource,
				string groupName,
				Action<string> onSuccess,
				Action<string> onFail,
				Action<string> onNotExist,
				Action<string> onAccessDenied,
				string user
		) {
			if (!_started) return;
			var key = BuildSubscriptionGroupKey(eventSource.ToString(), groupName);
			Log.Debug("Deleting persistent subscription {subscriptionKey}", key);

			PersistentSubscription subscription;
			if (!_subscriptionsById.TryGetValue(key, out subscription)) {
				onNotExist($"Group '{groupName}' does not exist.");
				return;
			}

			if (!_subscriptionTopics.ContainsKey(eventSource.ToString())) {
				onFail($"Group '{groupName}' does not exist.");
				return;
			}

			RemoveSubscription(eventSource.ToString(), groupName);
			RemoveSubscriptionConfig(user, eventSource.ToString(), groupName);
			subscription.Delete();
			SaveConfiguration(() => onSuccess(""));
		}


		public void Handle(ClientMessage.DeletePersistentSubscriptionToStream message) {
			if (string.IsNullOrEmpty(message.EventStreamId) || message.EventStreamId == SystemStreams.AllStream) {
				message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionToStreamCompleted(
					message.CorrelationId,
					ClientMessage.DeletePersistentSubscriptionToStreamCompleted.DeletePersistentSubscriptionToStreamResult.Fail,
					"Bad stream name."));
				return;
			}

			DeletePersistentSubscription(
				new PersistentSubscriptionSingleStreamEventSource(message.EventStreamId),
				message.GroupName,
				(msg) => {
					message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionToStreamCompleted(
						message.CorrelationId,
						ClientMessage.DeletePersistentSubscriptionToStreamCompleted
							.DeletePersistentSubscriptionToStreamResult.Success, msg));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionToStreamCompleted(
						message.CorrelationId,
						ClientMessage.DeletePersistentSubscriptionToStreamCompleted.DeletePersistentSubscriptionToStreamResult.Fail,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionToStreamCompleted(
						message.CorrelationId,
						ClientMessage.DeletePersistentSubscriptionToStreamCompleted.DeletePersistentSubscriptionToStreamResult.DoesNotExist,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionToStreamCompleted(
						message.CorrelationId,
						ClientMessage.DeletePersistentSubscriptionToStreamCompleted.DeletePersistentSubscriptionToStreamResult.AccessDenied,
						error));
				},
				message.User?.Identity?.Name
			);
		}

		public void Handle(ClientMessage.DeletePersistentSubscriptionToAll message) {
			DeletePersistentSubscription(
				new PersistentSubscriptionAllStreamEventSource(),
				message.GroupName,
				(msg) => {
					message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionToAllCompleted(
						message.CorrelationId,
						ClientMessage.DeletePersistentSubscriptionToAllCompleted
							.DeletePersistentSubscriptionToAllResult.Success, msg));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionToAllCompleted(
						message.CorrelationId,
						ClientMessage.DeletePersistentSubscriptionToAllCompleted.DeletePersistentSubscriptionToAllResult.Fail,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionToAllCompleted(
						message.CorrelationId,
						ClientMessage.DeletePersistentSubscriptionToAllCompleted.DeletePersistentSubscriptionToAllResult.DoesNotExist,
						error));
				},
				(error) => {
					message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionToAllCompleted(
						message.CorrelationId,
						ClientMessage.DeletePersistentSubscriptionToAllCompleted.DeletePersistentSubscriptionToAllResult.AccessDenied,
						error));
				},
				message.User?.Identity?.Name
			);
		}

		private void RemoveSubscriptionConfig(string username, string eventSource, string groupName) {
			_config.Updated = DateTime.Now;
			_config.UpdatedBy = username;
			var index = _config.Entries.FindLastIndex(x => x.Stream == eventSource && x.Group == groupName);
			_config.Entries.RemoveAt(index);
		}

		private void RemoveSubscription(string eventSource, string groupName) {
			List<PersistentSubscription> subscribers;
			var key = BuildSubscriptionGroupKey(eventSource, groupName);
			_subscriptionsById.Remove(key);
			if (_subscriptionTopics.TryGetValue(eventSource, out subscribers)) {
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

		public void ConnectToPersistentSubscription(
			IPersistentSubscriptionEventSource eventSource,
			string groupName,
			int allowedInFlightMessages,
			Guid connectionId,
			string connectionName,
			string from,
			Guid correlationId,
			IEnvelope envelope,
			string user) {
			if (!_started) return;

			List<PersistentSubscription> subscribers;
			if (!_subscriptionTopics.TryGetValue(eventSource.ToString(), out subscribers)) {
				envelope.ReplyWith(new ClientMessage.SubscriptionDropped(correlationId,
					SubscriptionDropReason.NotFound));
				return;
			}

			var key = BuildSubscriptionGroupKey(eventSource.ToString(), groupName);
			PersistentSubscription subscription;
			if (!_subscriptionsById.TryGetValue(key, out subscription)) {
				envelope.ReplyWith(new ClientMessage.SubscriptionDropped(correlationId, SubscriptionDropReason.NotFound));
				return;
			}

			if (subscription.HasReachedMaxClientCount) {
				envelope.ReplyWith(new ClientMessage.SubscriptionDropped(correlationId,
					SubscriptionDropReason.SubscriberMaxCountReached));
				return;
			}

			Log.Debug("New connection to persistent subscription {subscriptionKey} by {connectionId}", key, connectionId);
			long? lastEventNumber = null;
			if (eventSource.FromStream) {
				var streamId = _readIndex.GetStreamId(eventSource.EventStreamId);
				lastEventNumber = _readIndex.GetStreamLastEventNumber(streamId);
			}
			var lastCommitPos = _readIndex.LastIndexedPosition;
			var subscribedMessage =
				new ClientMessage.PersistentSubscriptionConfirmation(key, correlationId, lastCommitPos, lastEventNumber);
			envelope.ReplyWith(subscribedMessage);
			var name = user ?? "anonymous";
			subscription.AddClient(correlationId, connectionId, connectionName, envelope,
				allowedInFlightMessages, name, from);

		}

		public void Handle(ClientMessage.ConnectToPersistentSubscriptionToStream message) {
			ConnectToPersistentSubscription(
				new PersistentSubscriptionSingleStreamEventSource(message.EventStreamId),
				message.GroupName,
				message.AllowedInFlightMessages,
				message.ConnectionId,
				message.ConnectionName,
				message.From,
				message.CorrelationId,
				message.Envelope,
				message.User?.Identity?.Name);
		}

		public void Handle(ClientMessage.ConnectToPersistentSubscriptionToAll message) {
			ConnectToPersistentSubscription(
				new PersistentSubscriptionAllStreamEventSource(),
				message.GroupName,
				message.AllowedInFlightMessages,
				message.ConnectionId,
				message.ConnectionName,
				message.From,
				message.CorrelationId,
				message.Envelope,
				message.User?.Identity?.Name);
		}

		private static string BuildSubscriptionGroupKey(string stream, string groupName) {
			return stream + "::" + groupName;
		}

		public void Handle(StorageMessage.EventCommitted message) {
			if (!_started) return;
			ProcessEventCommited(message.Event.EventStreamId, message.CommitPosition, message.Event);
		}

		private void ProcessEventCommited(string eventStreamId, long commitPosition, EventRecord evnt) {
			var subscriptions = new List<PersistentSubscription>();
			if (EventFilter.DefaultStreamFilter.IsEventAllowed(evnt)
				&& _subscriptionTopics.TryGetValue(eventStreamId, out var subscriptionsToStream)) {
				subscriptions.AddRange(subscriptionsToStream);
			}

			if (EventFilter.DefaultAllFilter.IsEventAllowed(evnt)
			    && _subscriptionTopics.TryGetValue(SystemStreams.AllStream, out var subscriptionsToAll)) {
				subscriptions.AddRange(subscriptionsToAll);
			}

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
					string[] parts = Helper.UTF8NoBom.GetString(eventRecord.Data.Span).Split('@');
					long eventNumber = long.Parse(parts[0]);
					string streamName = parts[1];
					var streamId = _readIndex.GetStreamId(streamName);
					var res = _readIndex.ReadEvent(streamName, streamId, eventNumber);
					if (res.Result == ReadEventResult.Success)
						return ResolvedEvent.ForResolvedLink(res.Record, eventRecord, commitPosition);

					return ResolvedEvent.ForFailedResolvedLink(eventRecord, res.Result, commitPosition);
				} catch (Exception exc) {
					Log.Error(exc, "Error while resolving link for event record: {eventRecord}",
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

			if (string.IsNullOrEmpty(message.EventStreamId) || message.EventStreamId == SystemStreams.AllStream) {
				message.Envelope.ReplyWith(new ClientMessage.ReadNextNPersistentMessagesCompleted(
					message.CorrelationId,
					ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult.Fail,
					"Bad stream name.", null));
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

		public void Handle(ClientMessage.ReplayParkedMessages message) {
			PersistentSubscription subscription;
			var key = BuildSubscriptionGroupKey(message.EventStreamId, message.GroupName);
			Log.Debug("Replaying parked messages for persistent subscription {subscriptionKey} {to}", 
				key,
				message.StopAt.HasValue ? $" (To: '{message.StopAt.ToString()}')" : " (All)");

			if (message.StopAt.HasValue && message.StopAt.Value < 0) {
				message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
					ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.Fail,
					"Cannot stop replaying parked message at a negative version."));
				return;
			}

			if (!_subscriptionsById.TryGetValue(key, out subscription)) {
				message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
					ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.DoesNotExist,
					"Unable to locate '" + key + "'"));
				return;
			}

			subscription.RetryParkedMessages(message.StopAt);
			message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
				ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.Success, ""));
		}

		public void Handle(ClientMessage.ReplayParkedMessage message) {
			var key = BuildSubscriptionGroupKey(message.EventStreamId, message.GroupName);
			PersistentSubscription subscription;

			if (!_subscriptionsById.TryGetValue(key, out subscription)) {
				message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
					ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.DoesNotExist,
					"Unable to locate '" + key + "'"));
				return;
			}

			subscription.RetrySingleParkedMessage(message.Event);
			message.Envelope.ReplyWith(new ClientMessage.ReplayMessagesReceived(message.CorrelationId,
				ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.Success, ""));
		}

		private void LoadConfiguration(Action continueWith) {
			_ioDispatcher.ReadBackward(SystemStreams.PersistentSubscriptionConfig, -1, 1, false,
				SystemAccounts.System, x => HandleLoadCompleted(continueWith, x));
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

							IPersistentSubscriptionEventSource eventSource;
							if (entry.Stream == SystemStreams.AllStream) {
								IEventFilter filter = null;
								if (entry.Filter != null) {
									var (success, reason) = EventFilter.TryParse(entry.Filter, out filter);
									if (!success) {
										Log.Error(
											"Could not load filtered persistent subscription to $all for group {group}. The filter could not be parsed: '{reason}",
											entry.Group, reason);
										continue;
									}
								}

								eventSource = new PersistentSubscriptionAllStreamEventSource(filter);
							} else {
								eventSource = new PersistentSubscriptionSingleStreamEventSource(entry.Stream);
							}

							CreateSubscriptionGroup(eventSource,
								entry.Group,
								entry.ResolveLinkTos,
								#pragma warning disable 612
								eventSource.GetStreamPositionFor(entry.StartPosition ?? entry.StartFrom.ToString()),
								#pragma warning restore 612
								entry.ExtraStatistics,
								entry.MaxRetryCount,
								entry.LiveBufferSize,
								entry.HistoryBufferSize,
								entry.ReadBatchSize,
								ToCheckPointAfterTimeout(entry.CheckPointAfter),
								entry.MinCheckPointCount,
								entry.MaxCheckPointCount,
								entry.MaxSubscriberCount,
								entry.NamedConsumerStrategy,
								ToMessageTimeout(entry.MessageTimeout));
						}

						continueWith();
					} catch (Exception ex) {
						Log.Error(ex, "There was an error loading configuration from storage.");
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
				ExpectedVersion.Any, streamMetadata, events, SystemAccounts.System,
				x => HandleSaveConfigurationCompleted(continueWith, x));
		}

		private void HandleSaveConfigurationCompleted(Action continueWith, ClientMessage.WriteEventsCompleted obj) {
			switch (obj.Result) {
				case OperationResult.Success:
					continueWith();
					break;
				case OperationResult.CommitTimeout:
				case OperationResult.PrepareTimeout:
					Log.Information("Timeout while trying to save persistent subscription configuration. Retrying");
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

			var stats = new List<MonitoringMessage.PersistentSubscriptionInfo>() {
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
			if (!_handleTick || _timerTickCorrelationId != message.CorrelationId) return;
			try {
				WakeSubscriptions();
			} finally {
				_timerTickCorrelationId = Guid.NewGuid();
				_bus.Publish(TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(1000),
					new PublishEnvelope(_bus),
					new SubscriptionMessage.PersistentSubscriptionTimerTick(_timerTickCorrelationId)));
			}
		}

		private void WakeSubscriptions() {
			var now = DateTime.UtcNow;

			foreach (var subscription in _subscriptionsById.Values) {
				subscription.NotifyClockTick(now);
			}
		}

		
		private TimeSpan ToCheckPointAfterTimeout(int milliseconds) {
			return milliseconds == 0 ? TimeSpan.MaxValue : TimeSpan.FromMilliseconds(milliseconds);
		}

		private TimeSpan ToMessageTimeout(int milliseconds) {
			return milliseconds == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(milliseconds);
		}
	}
}
