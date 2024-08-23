using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public interface ISubscriber {
	void Subscribe<T>(IAsyncHandle<T> handler) where T : Message;
	void Unsubscribe<T>(IAsyncHandle<T> handler) where T : Message;
}
