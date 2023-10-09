using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.XUnit.Tests;

class EnvelopePublisher : IPublisher {
	private readonly IEnvelope _envelope;

	public EnvelopePublisher(IEnvelope envelope) {
		_envelope = envelope;
	}

	public void Publish(Message message) {
		_envelope.ReplyWith(message);
	}
}
