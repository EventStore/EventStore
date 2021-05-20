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

			public readonly Guid InstanceCorrelationId;

			public StartCore(Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
			}
		}

		public class StopCore : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Guid QueueId { get; }

			public StopCore(Guid queueId) {
				QueueId = queueId;
			}
		}

		public class StopCoreTimeout : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Guid QueueId { get; }

			public StopCoreTimeout(Guid queueId) {
				QueueId = queueId;
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

			public string SubComponent { get; }
			public Guid InstanceCorrelationId { get; }

			public SubComponentStarted(string subComponent, Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
				SubComponent = subComponent;
			}
		}

		public class SubComponentStopped : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string SubComponent;

			public Guid QueueId { get; }

			public SubComponentStopped(string subComponent, Guid queueId) {
				SubComponent = subComponent;
				QueueId = queueId;
			}
		}
	}
}
