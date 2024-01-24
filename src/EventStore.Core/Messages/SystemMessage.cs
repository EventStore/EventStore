using System;
using EventStore.Core.Cluster;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;
using EndPoint = System.Net.EndPoint;

namespace EventStore.Core.Messages {
	public static partial class SystemMessage {
		[DerivedMessage(CoreMessage.System)]
		public partial class SystemInit : Message<SystemInit> {
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class SystemStart : Message<SystemStart> {
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class SystemCoreReady : Message<SystemCoreReady> {
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class SystemReady : Message<SystemReady> {
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class ServiceInitialized : Message<ServiceInitialized> {
			public readonly string ServiceName;

			public ServiceInitialized(string serviceName) {
				Ensure.NotNullOrEmpty(serviceName, "serviceName");
				ServiceName = serviceName;
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class SubSystemInitialized : Message<SubSystemInitialized> {
			public readonly string SubSystemName;

			public SubSystemInitialized(string subSystemName) {
				Ensure.NotNullOrEmpty(subSystemName, "subSystemName");
				SubSystemName = subSystemName;
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class WriteEpoch : Message<WriteEpoch> {
			public readonly int EpochNumber;
			public WriteEpoch(int epochNumber) {
				EpochNumber = epochNumber;
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class InitiateLeaderResignation : Message<InitiateLeaderResignation> {
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class RequestQueueDrained : Message<RequestQueueDrained> {
		}

		public interface IStateChangeMessage : Message {
			Guid       CorrelationId { get; }
			VNodeState State         { get; }
		}

		[DerivedMessage]
		public abstract partial class StateChangeMessage<T> : Message<T>, IStateChangeMessage where T : Message {
			protected StateChangeMessage(Guid correlationId, VNodeState state) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				State = state;
			}
			
			public Guid       CorrelationId { get; }
			public VNodeState State         { get; }
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomePreLeader : StateChangeMessage<BecomePreLeader> {
			public BecomePreLeader(Guid correlationId) : base(correlationId, VNodeState.PreLeader) {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeLeader : StateChangeMessage<BecomeLeader> {
			public BecomeLeader(Guid correlationId) : base(correlationId, VNodeState.Leader) {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeShuttingDown : StateChangeMessage<BecomeShuttingDown> {
			public readonly bool ShutdownHttp;
			public readonly bool ExitProcess;

			public BecomeShuttingDown(Guid correlationId, bool exitProcess, bool shutdownHttp) : base(correlationId,
				VNodeState.ShuttingDown) {
				ShutdownHttp = shutdownHttp;
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				ExitProcess = exitProcess;
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeShutdown : StateChangeMessage<BecomeShutdown> {
			public BecomeShutdown(Guid correlationId) : base(correlationId, VNodeState.Shutdown) {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeUnknown : StateChangeMessage<BecomeUnknown> {
			public BecomeUnknown(Guid correlationId)
				: base(correlationId, VNodeState.Unknown) {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeDiscoverLeader : StateChangeMessage<BecomeDiscoverLeader> {
			public BecomeDiscoverLeader(Guid correlationId)
				: base(correlationId, VNodeState.DiscoverLeader) {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeResigningLeader : StateChangeMessage<BecomeResigningLeader> {
			public BecomeResigningLeader(Guid correlationId)
				: base(correlationId, VNodeState.ResigningLeader) {
			}
		}

		public interface IReplicaStateMessage : IStateChangeMessage {
			MemberInfo Leader { get; }
		}

		[DerivedMessage]
		public abstract partial class ReplicaStateMessage<T> : StateChangeMessage<T>, IReplicaStateMessage where T : Message {
			protected ReplicaStateMessage(Guid correlationId, VNodeState state, MemberInfo leader)
				: base(correlationId, state) {
				Ensure.NotNull(leader, "leader");
				Leader = leader;
			}

			public MemberInfo Leader { get; }
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomePreReplica : ReplicaStateMessage<BecomePreReplica> {
			public readonly Guid LeaderConnectionCorrelationId;

			public BecomePreReplica(Guid correlationId, Guid leaderConnectionCorrelationId, MemberInfo leader)
				: base(correlationId, VNodeState.PreReplica, leader) {
				LeaderConnectionCorrelationId = leaderConnectionCorrelationId;
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeCatchingUp : ReplicaStateMessage<BecomeCatchingUp> {
			public BecomeCatchingUp(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.CatchingUp,
				leader) {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeClone : ReplicaStateMessage<BecomeClone> {
			public BecomeClone(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.Clone, leader) {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeFollower : ReplicaStateMessage<BecomeFollower> {
			public BecomeFollower(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.Follower,
				leader) {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeReadOnlyLeaderless : StateChangeMessage<BecomeReadOnlyLeaderless> {
			public BecomeReadOnlyLeaderless(Guid correlationId)
				: base(correlationId, VNodeState.ReadOnlyLeaderless) {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomePreReadOnlyReplica : ReplicaStateMessage<BecomePreReadOnlyReplica> {
			public readonly Guid LeaderConnectionCorrelationId;

			public BecomePreReadOnlyReplica(Guid correlationId, Guid leaderConnectionCorrelationId, MemberInfo leader)
				: base(correlationId, VNodeState.PreReadOnlyReplica, leader) {
				LeaderConnectionCorrelationId = leaderConnectionCorrelationId;
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class BecomeReadOnlyReplica : ReplicaStateMessage<BecomeReadOnlyReplica> {
			public BecomeReadOnlyReplica(Guid correlationId, MemberInfo leader)
				: base(correlationId, VNodeState.ReadOnlyReplica, leader) {
			}
		}


		[DerivedMessage(CoreMessage.System)]
		public partial class ServiceShutdown : Message<ServiceShutdown> {
			public readonly string ServiceName;

			public ServiceShutdown(string serviceName) {
				if (String.IsNullOrEmpty(serviceName))
					throw new ArgumentNullException("serviceName");
				ServiceName = serviceName;
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class ShutdownTimeout : Message<ShutdownTimeout> {
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class VNodeConnectionLost : Message<VNodeConnectionLost> {
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

		[DerivedMessage(CoreMessage.System)]
		public partial class VNodeConnectionEstablished : Message<VNodeConnectionEstablished> {
			public readonly EndPoint VNodeEndPoint;
			public readonly Guid ConnectionId;

			public VNodeConnectionEstablished(EndPoint vNodeEndPoint, Guid connectionId) {
				Ensure.NotNull(vNodeEndPoint, "vNodeEndPoint");
				Ensure.NotEmptyGuid(connectionId, "connectionId");

				VNodeEndPoint = vNodeEndPoint;
				ConnectionId = connectionId;
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class WaitForChaserToCatchUp : Message<WaitForChaserToCatchUp> {
			public readonly Guid CorrelationId;
			public readonly TimeSpan TotalTimeWasted;

			public WaitForChaserToCatchUp(Guid correlationId, TimeSpan totalTimeWasted) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");

				CorrelationId = correlationId;
				TotalTimeWasted = totalTimeWasted;
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class ChaserCaughtUp : Message<ChaserCaughtUp> {
			public readonly Guid CorrelationId;

			public ChaserCaughtUp(Guid correlationId) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class EnablePreLeaderReplication : Message<EnablePreLeaderReplication> {
			public EnablePreLeaderReplication() {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class CheckInaugurationConditions : Message<CheckInaugurationConditions> {
			public CheckInaugurationConditions() {
			}
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class RequestForwardingTimerTick : Message<RequestForwardingTimerTick> {
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class NoQuorumMessage : Message<NoQuorumMessage> {
		}

		[DerivedMessage(CoreMessage.System)]
		public partial class EpochWritten : Message<EpochWritten> {
			public readonly EpochRecord Epoch;

			public EpochWritten(EpochRecord epoch) {
				Ensure.NotNull(epoch, "epoch");
				Epoch = epoch;
			}
		}
	}
}
