using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messaging;

class PublishToWrapEnvelop(IPublisher publisher, IEnvelope nestedEnvelope) : IEnvelope {
	public void ReplyWith<T>(T message) where T : class, Message => 
		publisher.Publish(new UnwrapEnvelopeMessage(() => nestedEnvelope.ReplyWith(message)));
}
