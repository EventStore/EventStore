using EventStore.Core.Messaging;

namespace EventStore.ClientAPI.Embedded {
	internal class EmbeddedResponseEnvelope : IEnvelope {
		private readonly IEmbeddedResponder _responder;

		public EmbeddedResponseEnvelope(IEmbeddedResponder responder) {
			_responder = responder;
		}

		public void ReplyWith<T>(T message) where T : Message {
			_responder.InspectMessage(message);
		}
	}
}
