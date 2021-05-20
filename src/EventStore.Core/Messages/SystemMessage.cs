using System;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;
using EndPoint = System.Net.EndPoint;

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
			public readonly int EpochNumber;
			public WriteEpoch(int epochNumber) {
				EpochNumber = epochNumber;
			}


		}

		public class InitiateLeaderResignation : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class RequestQueueDrained : Message {
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

		public class BecomePreLeader : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomePreLeader(Guid correlationId) : base(correlationId, VNodeState.PreLeader) {
			}
		}

		public class BecomeLeader : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int EpochNumber;

			public BecomeLeader(Guid correlationId, int epochNumber) : base(correlationId, VNodeState.Leader) {
				EpochNumber = epochNumber;
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

		public class BecomeDiscoverLeader : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeDiscoverLeader(Guid correlationId)
				: base(correlationId, VNodeState.DiscoverLeader) {
			}
		}

		public class BecomeResigningLeader : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeResigningLeader(Guid correlationId)
				: base(correlationId, VNodeState.ResigningLeader) {
			}
		}

		public abstract class ReplicaStateMessage : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly MemberInfo Leader;

			protected ReplicaStateMessage(Guid correlationId, VNodeState state, MemberInfo leader)
				: base(correlationId, state) {
				Ensure.NotNull(leader, "leader");
				Leader = leader;
			}
		}

		public class BecomePreReplica : ReplicaStateMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomePreReplica(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.PreReplica,
				leader) {
			}
		}

		public class BecomeCatchingUp : ReplicaStateMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeCatchingUp(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.CatchingUp,
				leader) {
			}
		}

		public class BecomeClone : ReplicaStateMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeClone(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.Clone, leader) {
			}
		}

		public class BecomeFollower : ReplicaStateMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeFollower(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.Follower,
				leader) {
			}
		}

		public class BecomeReadOnlyLeaderless : StateChangeMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeReadOnlyLeaderless(Guid correlationId)
				: base(correlationId, VNodeState.ReadOnlyLeaderless) {
			}
		}

		public class BecomePreReadOnlyReplica : ReplicaStateMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomePreReadOnlyReplica(Guid correlationId, MemberInfo leader)
				: base(correlationId, VNodeState.PreReadOnlyReplica, leader) {
			}
		}

		public class BecomeReadOnlyReplica : ReplicaStateMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public BecomeReadOnlyReplica(Guid correlationId, MemberInfo leader)
				: base(correlationId, VNodeState.ReadOnlyReplica, leader) {
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

			public readonly EndPoint VNodeEndPoint;
			public readonly Guid ConnectionId;
			public readonly Guid? SubscriptionId;

			public VNodeConnectionLost(EndPoint vNodeEndPoint, Guid connectionId, Guid? subscriptionId = null) {
				Ensure.NotNull(vNodeEndPoint, "vNodeEndPoint");
				Ensure.NotEmptyGuid(connectionId, "connectionId");

				VNodeEndPoint = vNodeEndPoint;
				ConnectionId = connectionId;
				SubscriptionId = subscriptionId;
			}
		}

		public class VNodeConnectionEstablished : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly EndPoint VNodeEndPoint;
			public readonly Guid ConnectionId;

			public VNodeConnectionEstablished(EndPoint vNodeEndPoint, Guid connectionId) {
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
