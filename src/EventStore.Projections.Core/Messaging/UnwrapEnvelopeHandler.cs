using EventStore.Core.Bus;

namespace EventStore.Projections.Core.Messaging {
	public class UnwrapEnvelopeHandler : IHandle<UnwrapEnvelopeMessage> {
		public void Handle(UnwrapEnvelopeMessage message) {
			message.Action();
		}
	}
}
