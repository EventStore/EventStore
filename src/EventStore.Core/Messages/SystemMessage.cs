using System;
using System.Net;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages {
	public static class SystemMessage {
		public class SystemInit : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class SystemStart : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class SystemCoreReady : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class SystemReady : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class ServiceInitialized : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string ServiceName;

			public ServiceInitialized(string serviceName) {
				Ensure.NotNullOrEmpty(serviceName, "serviceName");
				ServiceName = serviceName;
			}
		}

		public class SubSystemInitialized : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string SubSystemName;

			public SubSystemInitialized(string subSystemName) {
				Ensure.NotNullOrEmpty(subSystemName, "subSystemName");
				SubSystemName = subSystemName;
			}
		}

		public class WriteEpoch : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public abstract class StateChangeMessage : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly VNodeState State;

			protected StateChangeMessage(Guid correlationId, VNodeState state) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				State = state;
			}
		}

		public class BecomePreMaster : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomePreMaster(Guid correlationId) : base(correlationId, VNodeState.PreMaster) {
			}
		}

		public class BecomeMaster : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeMaster(Guid correlationId) : base(correlationId, VNodeState.Master) {
			}
		}

		public class BecomeShuttingDown : StateChangeMessage {
			public readonly bool ShutdownHttp;
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly bool ExitProcess;

			public BecomeShuttingDown(Guid correlationId, bool exitProcess, bool shutdownHttp) : base(correlationId,
				VNodeState.ShuttingDown) {
				ShutdownHttp = shutdownHttp;
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				ExitProcess = exitProcess;
			}
		}

		public class BecomeShutdown : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeShutdown(Guid correlationId) : base(correlationId, VNodeState.Shutdown) {
			}
		}

		public class BecomeUnknown : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeUnknown(Guid correlationId)
				: base(correlationId, VNodeState.Unknown) {
			}
		}

		public abstract class ReplicaStateMessage : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly VNodeInfo Master;

			protected ReplicaStateMessage(Guid correlationId, VNodeState state, VNodeInfo master)
				: base(correlationId, state) {
				Ensure.NotNull(master, "master");
				Master = master;
			}
		}

		public class BecomePreReplica : ReplicaStateMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomePreReplica(Guid correlationId, VNodeInfo master) : base(correlationId, VNodeState.PreReplica,
				master) {
			}
		}

		public class BecomeCatchingUp : ReplicaStateMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeCatchingUp(Guid correlationId, VNodeInfo master) : base(correlationId, VNodeState.CatchingUp,
				master) {
			}
		}

		public class BecomeClone : ReplicaStateMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeClone(Guid correlationId, VNodeInfo master) : base(correlationId, VNodeState.Clone, master) {
			}
		}

		public class BecomeSlave : ReplicaStateMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeSlave(Guid correlationId, VNodeInfo master) : base(correlationId, VNodeState.Slave, master) {
			}
		}

		public class ServiceShutdown : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string ServiceName;

			public ServiceShutdown(string serviceName) {
				if (String.IsNullOrEmpty(serviceName))
					throw new ArgumentNullException("serviceName");
				ServiceName = serviceName;
			}
		}

		public class ShutdownTimeout : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class VNodeConnectionLost : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IPEndPoint VNodeEndPoint;
			public readonly Guid ConnectionId;

			public VNodeConnectionLost(IPEndPoint vNodeEndPoint, Guid connectionId) {
				Ensure.NotNull(vNodeEndPoint, "vNodeEndPoint");
				Ensure.NotEmptyGuid(connectionId, "connectionId");

				VNodeEndPoint = vNodeEndPoint;
				ConnectionId = connectionId;
			}
		}

		public class VNodeConnectionEstablished : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IPEndPoint VNodeEndPoint;
			public readonly Guid ConnectionId;

			public VNodeConnectionEstablished(IPEndPoint vNodeEndPoint, Guid connectionId) {
				Ensure.NotNull(vNodeEndPoint, "vNodeEndPoint");
				Ensure.NotEmptyGuid(connectionId, "connectionId");

				VNodeEndPoint = vNodeEndPoint;
				ConnectionId = connectionId;
			}
		}

		public class WaitForChaserToCatchUp : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly TimeSpan TotalTimeWasted;

			public WaitForChaserToCatchUp(Guid correlationId, TimeSpan totalTimeWasted) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");

				CorrelationId = correlationId;
				TotalTimeWasted = totalTimeWasted;
			}
		}

		public class ChaserCaughtUp : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public ChaserCaughtUp(Guid correlationId) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
			}
		}

		public class RequestForwardingTimerTick : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class NoQuorumMessage : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class EpochWritten : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly EpochRecord Epoch;

			public EpochWritten(EpochRecord epoch) {
				Ensure.NotNull(epoch, "epoch");
				Epoch = epoch;
			}
		}
	}
}
