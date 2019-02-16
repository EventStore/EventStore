using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Fakes {
	public class NoopPublisher : IPublisher {
		public void Publish(Message message) {
			// do nothing
		}
	}
}
