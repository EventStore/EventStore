using EventStore.Core.Messaging;
using EventStore.Core.Scanning;

namespace EventStore.Core.Tests.Bus.Helpers {
	public class TestMessage : Message {
		private static readonly int TypeId = SequenceId.Next();

		public override int MsgTypeId {
			get { return TypeId; }
		}
	}

	public class TestMessage2 : Message {
		private static readonly int TypeId = SequenceId.Next();

		public override int MsgTypeId {
			get { return TypeId; }
		}
	}

	public class TestMessage3 : Message {
		private static readonly int TypeId = SequenceId.Next();

		public override int MsgTypeId {
			get { return TypeId; }
		}
	}

	public class ParentTestMessage : Message {
		private static readonly int TypeId = SequenceId.Next();

		public override int MsgTypeId {
			get { return TypeId; }
		}
	}

	public class ChildTestMessage : ParentTestMessage {
		private static readonly int TypeId = SequenceId.Next();

		public override int MsgTypeId {
			get { return TypeId; }
		}
	}

	public class GrandChildTestMessage : ChildTestMessage {
		private static readonly int TypeId = SequenceId.Next();

		public override int MsgTypeId {
			get { return TypeId; }
		}
	}

	public class TestMessageWithId : Message {
		private static readonly int TypeId = SequenceId.Next();

		public override int MsgTypeId {
			get { return TypeId; }
		}

		public int Id;

		public TestMessageWithId(int id) {
			Id = id;
		}
	}
}
