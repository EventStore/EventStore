using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Common.Utils.Threading;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.Internal {
	internal class OperationItem {
		private static long _nextSeqNo = -1;
		public readonly long SeqNo = Interlocked.Increment(ref _nextSeqNo);

		public readonly IClientOperation Operation;
		public readonly int MaxRetries;
		public readonly TimeSpan Timeout;
		public readonly DateTime CreatedTime;

		public Guid ConnectionId;
		public Guid CorrelationId;
		public int RetryCount;
		public DateTime LastUpdated;

		public OperationItem(IClientOperation operation, int maxRetries, TimeSpan timeout) {
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
			return string.Format(
				"Operation {0} ({1:D}): {2}, retry count: {3}, created: {4:HH:mm:ss.fff}, last updated: {5:HH:mm:ss.fff}",
				Operation.GetType().Name, CorrelationId, Operation, RetryCount, CreatedTime, LastUpdated);
		}
	}

	internal class OperationsManager {
		private static readonly IComparer<OperationItem> SeqNoComparer = new OperationItemSeqNoComparer();

		public int TotalOperationCount {
			get { return _totalOperationCount; }
		}

		private readonly string _connectionName;
		private readonly ConnectionSettings _settings;
		private readonly Dictionary<Guid, OperationItem> _activeOperations = new Dictionary<Guid, OperationItem>();

		private readonly ConcurrentQueueWrapper<OperationItem> _waitingOperations =
			new ConcurrentQueueWrapper<OperationItem>();

		private readonly List<OperationItem> _retryPendingOperations = new List<OperationItem>();
		private readonly object _lock = new object();
		private int _totalOperationCount;

		public OperationsManager(string connectionName, ConnectionSettings settings) {
			Ensure.NotNull(connectionName, "connectionName");
			Ensure.NotNull(settings, "settings");
			_connectionName = connectionName;
			_settings = settings;
		}

		public bool TryGetActiveOperation(Guid correlationId, out OperationItem operation) {
			return _activeOperations.TryGetValue(correlationId, out operation);
		}

		public void CleanUp() {
			var connectionClosedException =
				new ConnectionClosedException(string.Format("Connection '{0}' was closed.", _connectionName));
			foreach (var operation in _activeOperations.Values
				.Concat(_waitingOperations)
				.Concat(_retryPendingOperations)) {
				operation.Operation.Fail(connectionClosedException);
			}

			_activeOperations.Clear();
			OperationItem dummy;
			while (_waitingOperations.TryDequeue(out dummy)) ;
			_retryPendingOperations.Clear();
			_totalOperationCount = 0;
		}

		public void CheckTimeoutsAndRetry(TcpPackageConnection connection) {
			var retryOperations = new List<OperationItem>();
			var removeOperations = new List<OperationItem>();
			foreach (var operation in _activeOperations.Values) {
				if (connection != null && operation.ConnectionId != connection.ConnectionId) {
					retryOperations.Add(operation);
				} else if (operation.Timeout > TimeSpan.Zero &&
				           DateTime.UtcNow - operation.LastUpdated > _settings.OperationTimeout) {
					var err = string.Format("EventStoreConnection '{0}': operation never got response from server.\n"
					                        + "UTC now: {1:HH:mm:ss.fff}, operation: {2}.",
						_connectionName, DateTime.UtcNow, operation);
					_settings.Log.Debug(err);

					if (_settings.FailOnNoServerResponse) {
						operation.Operation.Fail(new OperationTimedOutException(err));
						removeOperations.Add(operation);
					} else {
						retryOperations.Add(operation);
					}
				}
			}

			foreach (var operation in removeOperations) {
				RemoveOperation(operation);
			}

			if (connection == null) return;

			foreach (var operation in retryOperations) {
				ScheduleOperationRetry(operation);
			}

			if (_retryPendingOperations.Count > 0) {
				_retryPendingOperations.Sort(SeqNoComparer);
				foreach (var operation in _retryPendingOperations) {
					var oldCorrId = operation.CorrelationId;
					operation.CorrelationId = Guid.NewGuid();
					operation.RetryCount += 1;
					LogDebug("retrying, old corrId {0}, operation {1}.", oldCorrId, operation);
					ScheduleOperation(operation, connection);
				}

				_retryPendingOperations.Clear();
			}

			TryScheduleWaitingOperations(connection);
		}

		public void ScheduleOperationRetry(OperationItem operation) {
			if (!RemoveOperation(operation))
				return;

			LogDebug("ScheduleOperationRetry for {0}", operation);
			if (operation.MaxRetries >= 0 && operation.RetryCount >= operation.MaxRetries) {
				operation.Operation.Fail(new RetriesLimitReachedException(operation.ToString(), operation.RetryCount));
				return;
			}

			_retryPendingOperations.Add(operation);
		}

		public bool RemoveOperation(OperationItem operation) {
			if (!_activeOperations.Remove(operation.CorrelationId)) {
				LogDebug("RemoveOperation FAILED for {0}", operation);
				return false;
			}

			LogDebug("RemoveOperation SUCCEEDED for {0}", operation);
			_totalOperationCount = _activeOperations.Count + _waitingOperations.Count;
			return true;
		}

		public void TryScheduleWaitingOperations(TcpPackageConnection connection) {
			Ensure.NotNull(connection, "connection");
			lock (_lock) {
				// We don't want to transmit or retain expired requests, so we trim any from before the cutoff implied by the current time
				var cutoff = _settings.QueueTimeout == TimeSpan.Zero
					? (DateTime?)null
					: DateTime.UtcNow - _settings.QueueTimeout;

				OperationItem operation;
				while (_activeOperations.Count < _settings.MaxConcurrentItems) {
					if (!_waitingOperations.TryDequeue(out operation))
						break;
					if (cutoff == null || !TryExpireItem(cutoff.Value, operation))
						ExecuteOperation(operation, connection);
				}

				if (cutoff != null) {
					// In case the active operations queue is at capacity, we trim expired items from the front of the queue
					while (_waitingOperations.TryPeek(out operation) && TryExpireItem(cutoff.Value, operation)) {
						_waitingOperations.TryDequeue(out operation);
					}
				}

				_totalOperationCount = _activeOperations.Count + _waitingOperations.Count;
			}
		}

		bool TryExpireItem(DateTime cutoffDate, OperationItem operation) {
			if (operation.CreatedTime > cutoffDate)
				return false;

			var err = string.Format("EventStoreConnection '{0}': request expired.\n"
			                        + "UTC now: {1:HH:mm:ss.fff}, operation: {2}.",
				_connectionName, DateTime.UtcNow, operation);
			_settings.Log.Debug(err);
			operation.Operation.Fail(new OperationExpiredException(err));
			return true;
		}

		public void ExecuteOperation(OperationItem operation, TcpPackageConnection connection) {
			operation.ConnectionId = connection.ConnectionId;
			operation.LastUpdated = DateTime.UtcNow;
			_activeOperations.Add(operation.CorrelationId, operation);

			var package = operation.Operation.CreateNetworkPackage(operation.CorrelationId);
			LogDebug("ExecuteOperation package {0}, {1}, {2}.", package.Command, package.CorrelationId, operation);
			connection.EnqueueSend(package);
		}

		public void EnqueueOperation(OperationItem operation) {
			LogDebug("EnqueueOperation WAITING for {0}.", operation);
			_waitingOperations.Enqueue(operation);
		}

		public void ScheduleOperation(OperationItem operation, TcpPackageConnection connection) {
			Ensure.NotNull(connection, "connection");
			_waitingOperations.Enqueue(operation);
			TryScheduleWaitingOperations(connection);
		}

		private void LogDebug(string message, params object[] parameters) {
			if (_settings.VerboseLogging)
				_settings.Log.Debug("EventStoreConnection '{0}': {1}.", _connectionName,
					parameters.Length == 0 ? message : string.Format(message, parameters));
		}

		internal class OperationItemSeqNoComparer : IComparer<OperationItem> {
			public int Compare(OperationItem x, OperationItem y) {
				return x.SeqNo.CompareTo(y.SeqNo);
			}
		}
	}
}
