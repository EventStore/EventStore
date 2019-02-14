using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {
	public interface IHandle<T> where T : Message {
		void Handle(T message);
	}
}
