using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.Internal {
	internal interface IEventStoreTransactionConnection {
		/// <summary>
		/// Writes to a transaction in the event store asynchronously
		/// </summary>
		/// <remarks>
		/// A <see cref="EventStoreTransaction"/> allows the calling of multiple writes with multiple
		/// round trips over long periods of time between the caller and the event store. This method
		/// is only available through the TCP interface and no equivalent exists for the RESTful interface.
		/// </remarks>
		/// <param name="transaction">The <see cref="EventStoreTransaction"/> to write to.</param>
		/// <param name="events">The events to write</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <returns>A <see cref="Task"/> allowing the caller to control the async operation</returns>
		Task TransactionalWriteAsync(EventStoreTransaction transaction, IEnumerable<EventData> events,
			UserCredentials userCredentials = null);

		/// <summary>
		/// Commits a multi-write transaction in the Event Store
		/// </summary>
		/// <param name="transaction">The <see cref="EventStoreTransaction"></see> to commit</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		Task<WriteResult> CommitTransactionAsync(EventStoreTransaction transaction,
			UserCredentials userCredentials = null);
	}
}
