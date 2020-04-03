using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IEventFilter {
		bool IsEventAllowed(EventRecord eventRecord);
	}
}
