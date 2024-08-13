using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {
	public interface IHandle<in T> where T : Message {
		void Handle(T message);
	}
}
