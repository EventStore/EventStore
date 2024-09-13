using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public interface IPublisher : IHandle<Message> {
	void Publish(Message message);

	void IHandle<Message>.Handle(Message message) => Publish(message);
}
