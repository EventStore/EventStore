using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.Internal {
	internal class SubscriptionItem {
		public readonly ISubscriptionOperation Operation;
		public readonly int MaxRetries;
		public readonly TimeSpan Timeout;
		public readonly DateTime CreatedTime;

		public Guid ConnectionId;
		public Guid CorrelationId;
		public bool IsSubscribed;
		public int RetryCount;
		public DateTime LastUpdated;

		public SubscriptionItem(ISubscriptionOperation operation, int maxRetries, TimeSpan timeout) {
			Ensure.NotNull(operation, "operation");

			Operation = operation;
			MaxRetries = maxRetries;
			Timeout = timeout;
			CreatedTime = DateTime.UtcNow;

			CorrelationId = Guid.NewGuid();
			RetryCount = 0;
			LastUpdated = DateTime.UtcNow;
		}

		public override string ToString() {
			return string.Format("Subscription {0} ({1:D}): {2}, is subscribed: {3}, retry count: {4}, "
			                     + "created: {5:HH:mm:ss.fff}, last updated: {6:HH:mm:ss.fff}",
				Operation.GetType().Name, CorrelationId, Operation, IsSubscribed, RetryCount, CreatedTime, LastUpdated);
		}
	}

	internal class SubscriptionsManager {
		private readonly string _connectionName;
		private readonly ConnectionSettings _settings;

		private readonly Dictionary<Guid, SubscriptionItem> _activeSubscriptions =
			new Dictionary<Guid, SubscriptionItem>();

		private readonly Queue<SubscriptionItem> _waitingSubscriptions = new Queue<SubscriptionItem>();
		private readonly List<SubscriptionItem> _retryPendingSubscriptions = new List<SubscriptionItem>();

		public SubscriptionsManager(string connectionName, ConnectionSettings settings) {
			Ensure.NotNull(connectionName, "connectionName");
			Ensure.NotNull(settings, "settings");
			_connectionName = connectionName;
			_settings = settings;
		}

		public bool TryGetActiveSubscription(Guid correlationId, out SubscriptionItem subscription) {
			return _activeSubscriptions.TryGetValue(correlationId, out subscription);
		}

		public void CleanUp() {
			var connectionClosedException =
				new ConnectionClosedException(string.Format("Connection '{0}' was closed.", _connectionName));
			foreach (var subscription in _activeSubscriptions.Values
				.Concat(_waitingSubscriptions)
				.Concat(_retryPendingSubscriptions)) {
				subscription.Operation.DropSubscription(SubscriptionDropReason.ConnectionClosed,
					connectionClosedException);
			}

			_activeSubscriptions.Clear();
			_waitingSubscriptions.Clear();
			_retryPendingSubscriptions.Clear();
		}

		public void PurgeSubscribedAndDroppedSubscriptions(Guid connectionId) {
			var subscriptionsToRemove = new List<SubscriptionItem>();
			foreach (var subscription in _activeSubscriptions.Values.Where(x =>
				x.IsSubscribed && x.ConnectionId == connectionId)) {
				subscription.Operation.ConnectionClosed();
				subscriptionsToRemove.Add(subscription);
			}

			foreach (var subscription in subscriptionsToRemove) {
				_activeSubscriptions.Remove(subscription.CorrelationId);
			}
		}

		public void CheckTimeoutsAndRetry(TcpPackageConnection connection) {
			Ensure.NotNull(connection, "connection");

			var retrySubscriptions = new List<SubscriptionItem>();
			var removeSubscriptions = new List<SubscriptionItem>();
			foreach (var subscription in _activeSubscriptions.Values) {
				if (subscription.IsSubscribed) continue;
				if (subscription.ConnectionId != connection.ConnectionId) {
					retrySubscriptions.Add(subscription);
				} else if (subscription.Timeout > TimeSpan.Zero &&
				           DateTime.UtcNow - subscription.LastUpdated > _settings.OperationTimeout) {
					var err = String.Format(
						"EventStoreConnection '{0}': subscription never got confirmation from server.\n" +
						"UTC now: {1:HH:mm:ss.fff}, operation: {2}.",
						_connectionName, DateTime.UtcNow, subscription);
					_settings.Log.Error(err);

					if (_settings.FailOnNoServerResponse) {
						subscription.Operation.DropSubscription(SubscriptionDropReason.SubscribingError,
							new OperationTimedOutException(err));
						removeSubscriptions.Add(subscription);
					} else {
						retrySubscriptions.Add(subscription);
					}
				}
			}

			foreach (var subscription in retrySubscriptions) {
				ScheduleSubscriptionRetry(subscription);
			}

			foreach (var subscription in removeSubscriptions) {
				RemoveSubscription(subscription);
			}

			if (_retryPendingSubscriptions.Count > 0) {
				foreach (var subscription in _retryPendingSubscriptions) {
					subscription.RetryCount += 1;
					StartSubscription(subscription, connection);
				}

				_retryPendingSubscriptions.Clear();
			}

			while (_waitingSubscriptions.Count > 0) {
				StartSubscription(_waitingSubscriptions.Dequeue(), connection);
			}
		}

		public bool RemoveSubscription(SubscriptionItem subscription) {
			var res = _activeSubscriptions.Remove(subscription.CorrelationId);
			LogDebug("RemoveSubscription {0}, result {1}.", subscription, res);
			return res;
		}

		public void ScheduleSubscriptionRetry(SubscriptionItem subscription) {
			if (!RemoveSubscription(subscription)) {
				LogDebug("RemoveSubscription failed when trying to retry {0}.", subscription);
				return;
			}

			if (subscription.MaxRetries >= 0 && subscription.RetryCount >= subscription.MaxRetries) {
				LogDebug("RETRIES LIMIT REACHED when trying to retry {0}.", subscription);
				subscription.Operation.DropSubscription(SubscriptionDropReason.SubscribingError,
					new RetriesLimitReachedException(subscription.ToString(), subscription.RetryCount));
				return;
			}

			LogDebug("retrying subscription {0}.", subscription);
			_retryPendingSubscriptions.Add(subscription);
		}

		public void EnqueueSubscription(SubscriptionItem subscriptionItem) {
			_waitingSubscriptions.Enqueue(subscriptionItem);
		}

		public void StartSubscription(SubscriptionItem subscription, TcpPackageConnection connection) {
			Ensure.NotNull(connection, "connection");

			if (subscription.IsSubscribed) {
				LogDebug("StartSubscription REMOVING due to already subscribed {0}.", subscription);
				RemoveSubscription(subscription);
				return;
			}

			subscription.CorrelationId = Guid.NewGuid();
			subscription.ConnectionId = connection.ConnectionId;
			subscription.LastUpdated = DateTime.UtcNow;

			_activeSubscriptions.Add(subscription.CorrelationId, subscription);

			if (!subscription.Operation.Subscribe(subscription.CorrelationId, connection)) {
				LogDebug("StartSubscription REMOVING AS COULD NOT SUBSCRIBE {0}.", subscription);
				RemoveSubscription(subscription);
			} else {
				LogDebug("StartSubscription SUBSCRIBING {0}.", subscription);
			}
		}

		private void LogDebug(string message, params object[] parameters) {
			if (_settings.VerboseLogging)
				_settings.Log.Debug("EventStoreConnection '{0}': {1}.", _connectionName,
					parameters.Length == 0 ? message : string.Format(message, parameters));
		}
	}
}
