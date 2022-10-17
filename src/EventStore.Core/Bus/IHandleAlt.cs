using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {
	public interface IHandleAlt<T> : IHandle<T> where T : Message {
		bool HandlesAlt { get; }
	}
}
