using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public interface IPublisher : IHandle<Message>, IEnvelope {
	void Publish(Message message);

	void IHandle<Message>.Handle(Message message) => Publish(message);

	void IEnvelope<Message>.ReplyWith<T>(T message) => Publish(message);
}
