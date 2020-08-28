using EventStore.Core.Data;
using EventStore.Core.TransactionLog.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IEventFilter {
		bool IsEventAllowed(EventRecord eventRecord);
	}
}
