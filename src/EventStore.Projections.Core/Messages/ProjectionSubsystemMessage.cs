using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	public static class ProjectionSubsystemMessage {

		public class RestartSubsystem : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public IEnvelope ReplyEnvelope { get; }

			public RestartSubsystem(IEnvelope replyEnvelope) {
				ReplyEnvelope = replyEnvelope;
			}
		}

		public class InvalidSubsystemRestart : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public string SubsystemState { get; }

			public InvalidSubsystemRestart(string subsystemState) {
				SubsystemState = subsystemState;
			}
		}

		public class SubsystemRestarting : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class StartComponents : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Guid InstanceCorrelationId { get; }

			public StartComponents(Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
			}
		}

		public class ComponentStarted : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public string ComponentName { get; }
			public Guid InstanceCorrelationId { get; }

			public ComponentStarted(string componentName, Guid instanceCorrelationId) {
				ComponentName = componentName;
				InstanceCorrelationId = instanceCorrelationId;
			}
		}

		public class StopComponents : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Guid InstanceCorrelationId { get; }

			public StopComponents(Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
			}
		}

		public class ComponentStopped : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public string ComponentName { get; }
			public Guid InstanceCorrelationId { get; }

			public ComponentStopped(string componentName, Guid instanceCorrelationId) {
				ComponentName = componentName;
				InstanceCorrelationId = instanceCorrelationId;
			}
		}

		public class IODispatcherDrained : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public string ComponentName { get; }

			public IODispatcherDrained(string componentName) {
				ComponentName = componentName;
			}
		}
	}
}
