using EventStore.Core.Data;
using EventStore.Core.TransactionLogV2.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IEventFilter {
		bool IsEventAllowed(EventRecord eventRecord);
	}
}
