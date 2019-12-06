using EventStore.Core.Data;

namespace EventStore.Core.Util {
	public interface IEventFilter {
		bool IsEventAllowed(EventRecord eventRecord);
	}
}
