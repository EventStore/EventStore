using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public interface IPublisher : IHandle<Message> {
	void Publish(Message message);

	void IHandle<Message>.Handle(Message message) => Publish(message);
}

/// <summary>
/// Marks <see cref="IPublisher"/> as being OK for
/// cross-thread publishing (e.g. in replying to envelopes).
/// </summary>
public interface IThreadSafePublisher;
