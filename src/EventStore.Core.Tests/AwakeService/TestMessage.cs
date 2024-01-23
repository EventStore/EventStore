using System.Threading;
using EventStore.Core.Messaging;
using EventStore.Core.Scanning;

namespace EventStore.Core.Tests.AwakeService {
	public class TestMessage : Message {
		private static readonly int TypeId = SequenceId.Next();

		public override int MsgTypeId {
			get { return TypeId; }
		}

		public readonly int Kind;

		public TestMessage(int kind) {
			Kind = kind;
		}
	}
}
