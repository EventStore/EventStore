using System.Threading;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.AwakeService {
	public class TestMessage : Message {
		private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		public readonly int Kind;

		public TestMessage(int kind) {
			Kind = kind;
		}
	}
}
