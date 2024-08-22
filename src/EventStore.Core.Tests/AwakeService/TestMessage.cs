using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.AwakeService {
	public class TestMessage : Message {
		public readonly int Kind;

		public TestMessage(int kind) {
			Kind = kind;
		}
	}
}
