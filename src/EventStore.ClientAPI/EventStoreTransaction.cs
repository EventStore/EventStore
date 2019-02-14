using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents a multi-request transaction with the Event Store
	/// </summary>
	public class EventStoreTransaction : IDisposable {
		/// <summary>
		/// The ID of the transaction. This can be used to recover
		/// a transaction later.
		/// </summary>
		public readonly long TransactionId;

		private readonly UserCredentials _userCredentials;
		private readonly IEventStoreTransactionConnection _connection;
		private bool _isRolledBack;
		private bool _isCommitted;

		/// <summary>
		/// Constructs a new <see cref="EventStoreTransaction"/>
		/// </summary>
		/// <param name="transactionId">The transaction id of the transaction</param>
		/// <param name="userCredentials">User credentials under which transaction is committed.</param>
		/// <param name="connection">The connection the transaction is hooked to</param>
		internal EventStoreTransaction(long transactionId, UserCredentials userCredentials,
			IEventStoreTransactionConnection connection) {
			Ensure.Nonnegative(transactionId, "transactionId");

			TransactionId = transactionId;
			_userCredentials = userCredentials;
			_connection = connection;
		}

		/// <summary>
		/// Asynchronously commits this transaction
		/// </summary>
		/// <returns>A <see cref="Task"/> that returns expected version for following write requests</returns>
		public Task<WriteResult> CommitAsync() {
			if (_isRolledBack) throw new InvalidOperationException("Cannot commit a rolledback transaction");
			if (_isCommitted) throw new InvalidOperationException("Transaction is already committed");
			_isCommitted = true;
			return _connection.CommitTransactionAsync(this, _userCredentials);
		}

		/// <summary>
		/// Writes to a transaction in the event store asynchronously
		/// </summary>
		/// <param name="events">The events to write</param>
		/// <returns>A <see cref="Task"/> allowing the caller to control the async operation</returns>
		public Task WriteAsync(params EventData[] events) {
			return WriteAsync((IEnumerable<EventData>)events);
		}

		/// <summary>
		/// Writes to a transaction in the event store asynchronously
		/// </summary>
		/// <param name="events">The events to write</param>
		/// <returns>A <see cref="Task"/> allowing the caller to control the async operation</returns>
		public Task WriteAsync(IEnumerable<EventData> events) {
			if (_isRolledBack) throw new InvalidOperationException("Cannot write to a rolled-back transaction");
			if (_isCommitted) throw new InvalidOperationException("Transaction is already committed");
			return _connection.TransactionalWriteAsync(this, events);
		}

		/// <summary>
		/// Rollsback this transaction.
		/// </summary>
		public void Rollback() {
			if (_isCommitted) throw new InvalidOperationException("Transaction is already committed");
			_isRolledBack = true;
		}

		/// <summary>
		/// Disposes this transaction rolling it back if not already committed
		/// </summary>
		public void Dispose() {
			if (!_isCommitted)
				_isRolledBack = true;
		}
	}
}
