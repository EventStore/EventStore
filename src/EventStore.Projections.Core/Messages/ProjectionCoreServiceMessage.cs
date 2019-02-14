using System;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.Projections.Core.Messages {
	public static partial class ProjectionCoreServiceMessage {
		public class StartCore : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid EpochId;

			public StartCore(Guid epochId) {
				EpochId = epochId;
			}
		}

		public class StopCore : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class Connected : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly TcpConnectionManager _connection;

			public Connected(TcpConnectionManager connection) {
				_connection = connection;
			}

			public TcpConnectionManager Connection {
				get { return _connection; }
			}
		}

		public class CoreTick : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly Action _action;

			public CoreTick(Action action) {
				_action = action;
			}

			public Action Action {
				get { return _action; }
			}
		}

		public class SubComponentStarted : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string SubComponent;

			public SubComponentStarted(string subComponent) {
				SubComponent = subComponent;
			}
		}

		public class SubComponentStopped : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string SubComponent;

			public SubComponentStopped(string subComponent) {
				SubComponent = subComponent;
			}
		}
	}
}
