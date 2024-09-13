using System.Diagnostics.CodeAnalysis;
using EventStore.Core.Bus;

namespace EventStore.Core.Messaging;

public class PublishEnvelope(IPublisher publisher) : IEnvelope {
	public void ReplyWith<T>(T message) where T : Message
		=> publisher.Publish(message);

	[ExcludeFromCodeCoverage]
	private object DebugView => publisher;
}
