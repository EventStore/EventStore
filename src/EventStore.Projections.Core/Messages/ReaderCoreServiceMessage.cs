using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	public static class ReaderCoreServiceMessage {
		public class StartReader : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class StopReader : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class ReaderTick : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly Action _action;

			public ReaderTick(Action action) {
				_action = action;
			}

			public Action Action {
				get { return _action; }
			}
		}
	}
}
