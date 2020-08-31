using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Bus.Helpers {
	public class NoopConsumer : IHandle<Message> {
		public void Handle(Message message) {
		}
	}
}
