using EventStore.Core.Messaging;
using EventStore.Core.Scanning;

namespace EventStore.Core.Tests.Bus.Helpers {
	public class TestMessage : Message<TestMessage> {
		private static readonly int TypeId = SequenceId.Next();

		public new int MsgTypeId => TypeId;
	}

	public class TestMessage2 : Message<TestMessage2> {
		private static readonly int TypeId = SequenceId.Next();

		public new int MsgTypeId => TypeId;
	}

	public class TestMessage3 : Message<TestMessage3> {
		private static readonly int TypeId = SequenceId.Next();

		public new int MsgTypeId => TypeId;
	}

	public class ParentTestMessage : Message<ParentTestMessage> {
		private static readonly int TypeId = SequenceId.Next();

		public new int MsgTypeId => TypeId;
	}

	public class ChildTestMessage : ParentTestMessage {
		private static readonly int TypeId = SequenceId.Next();

		public new int MsgTypeId => TypeId;
	}

	public class GrandChildTestMessage : ChildTestMessage {
		private static readonly int TypeId = SequenceId.Next();

		public new int MsgTypeId => TypeId;
	}

	public class TestMessageWithId : Message<TestMessageWithId> {
		private static readonly int TypeId = SequenceId.Next();

		public new int MsgTypeId => TypeId;

		public int Id;

		public TestMessageWithId(int id) {
			Id = id;
		}
	}
}
