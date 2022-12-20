using System;
using EventStore.Core.Cluster;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;
using EndPoint = System.Net.EndPoint;

namespace EventStore.Core.Messages {
	public static partial class SystemMessage {
		[DerivedMessage]
		public partial class SystemInit : Message {
		}

		[DerivedMessage]
		public partial class SystemStart : Message {
		}

		[DerivedMessage]
		public partial class SystemCoreReady : Message {
		}

		[DerivedMessage]
		public partial class SystemReady : Message {
		}

		[DerivedMessage]
		public partial class ServiceInitialized : Message {
			public readonly string ServiceName;

			public ServiceInitialized(string serviceName) {
				Ensure.NotNullOrEmpty(serviceName, "serviceName");
				ServiceName = serviceName;
			}
		}

		[DerivedMessage]
		public partial class SubSystemInitialized : Message {
			public readonly string SubSystemName;

			public SubSystemInitialized(string subSystemName) {
				Ensure.NotNullOrEmpty(subSystemName, "subSystemName");
				SubSystemName = subSystemName;
			}
		}

		[DerivedMessage]
		public partial class WriteEpoch : Message {
			public readonly int EpochNumber;
			public WriteEpoch(int epochNumber) {
				EpochNumber = epochNumber;
			}
		}

		[DerivedMessage]
		public partial class InitiateLeaderResignation : Message {
		}

		[DerivedMessage]
		public partial class RequestQueueDrained : Message {
		}

		[DerivedMessage]
		public abstract partial class StateChangeMessage : Message {
			public readonly Guid CorrelationId;
			public readonly VNodeState State;

			protected StateChangeMessage(Guid correlationId, VNodeState state) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				State = state;
			}
		}

		[DerivedMessage]
		public partial class BecomePreLeader : StateChangeMessage {
			public BecomePreLeader(Guid correlationId) : base(correlationId, VNodeState.PreLeader) {
			}
		}

		[DerivedMessage]
		public partial class BecomeLeader : StateChangeMessage {
			public BecomeLeader(Guid correlationId) : base(correlationId, VNodeState.Leader) {
			}
		}

		[DerivedMessage]
		public partial class BecomeShuttingDown : StateChangeMessage {
			public readonly bool ShutdownHttp;
			public readonly bool ExitProcess;

			public BecomeShuttingDown(Guid correlationId, bool exitProcess, bool shutdownHttp) : base(correlationId,
				VNodeState.ShuttingDown) {
				ShutdownHttp = shutdownHttp;
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				ExitProcess = exitProcess;
			}
		}

		[DerivedMessage]
		public partial class BecomeShutdown : StateChangeMessage {
			public BecomeShutdown(Guid correlationId) : base(correlationId, VNodeState.Shutdown) {
			}
		}

		[DerivedMessage]
		public partial class BecomeUnknown : StateChangeMessage {
			public BecomeUnknown(Guid correlationId)
				: base(correlationId, VNodeState.Unknown) {
			}
		}

		[DerivedMessage]
		public partial class BecomeDiscoverLeader : StateChangeMessage {
			public BecomeDiscoverLeader(Guid correlationId)
				: base(correlationId, VNodeState.DiscoverLeader) {
			}
		}

		[DerivedMessage]
		public partial class BecomeResigningLeader : StateChangeMessage {
			public BecomeResigningLeader(Guid correlationId)
				: base(correlationId, VNodeState.ResigningLeader) {
			}
		}

		[DerivedMessage]
		public abstract partial class ReplicaStateMessage : StateChangeMessage {
			public readonly MemberInfo Leader;

			protected ReplicaStateMessage(Guid correlationId, VNodeState state, MemberInfo leader)
				: base(correlationId, state) {
				Ensure.NotNull(leader, "leader");
				Leader = leader;
			}
		}

		[DerivedMessage]
		public partial class BecomePreReplica : ReplicaStateMessage {
			public readonly Guid LeaderConnectionCorrelationId;

			public BecomePreReplica(Guid correlationId, Guid leaderConnectionCorrelationId, MemberInfo leader)
				: base(correlationId, VNodeState.PreReplica, leader) {
				LeaderConnectionCorrelationId = leaderConnectionCorrelationId;
			}
		}

		[DerivedMessage]
		public partial class BecomeCatchingUp : ReplicaStateMessage {
			public BecomeCatchingUp(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.CatchingUp,
				leader) {
			}
		}

		[DerivedMessage]
		public partial class BecomeClone : ReplicaStateMessage {
			public BecomeClone(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.Clone, leader) {
			}
		}

		[DerivedMessage]
		public partial class BecomeFollower : ReplicaStateMessage {
			public BecomeFollower(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.Follower,
				leader) {
			}
		}

		[DerivedMessage]
		public partial class BecomeReadOnlyLeaderless : StateChangeMessage {
			public BecomeReadOnlyLeaderless(Guid correlationId)
				: base(correlationId, VNodeState.ReadOnlyLeaderless) {
			}
		}

		[DerivedMessage]
		public partial class BecomePreReadOnlyReplica : ReplicaStateMessage {
			public readonly Guid LeaderConnectionCorrelationId;

			public BecomePreReadOnlyReplica(Guid correlationId, Guid leaderConnectionCorrelationId, MemberInfo leader)
				: base(correlationId, VNodeState.PreReadOnlyReplica, leader) {
				LeaderConnectionCorrelationId = leaderConnectionCorrelationId;
			}
		}

		[DerivedMessage]
		public partial class BecomeReadOnlyReplica : ReplicaStateMessage {
			public BecomeReadOnlyReplica(Guid correlationId, MemberInfo leader)
				: base(correlationId, VNodeState.ReadOnlyReplica, leader) {
			}
		}


		[DerivedMessage]
		public partial class ServiceShutdown : Message {
			public readonly string ServiceName;

			public ServiceShutdown(string serviceName) {
				if (String.IsNullOrEmpty(serviceName))
					throw new ArgumentNullException("serviceName");
				ServiceName = serviceName;
			}
		}

		[DerivedMessage]
		public partial class ShutdownTimeout : Message {
		}

		[DerivedMessage]
		public partial class VNodeConnectionLost : Message {
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

		[DerivedMessage]
		public partial class VNodeConnectionEstablished : Message {
			public readonly EndPoint VNodeEndPoint;
			public readonly Guid ConnectionId;

			public VNodeConnectionEstablished(EndPoint vNodeEndPoint, Guid connectionId) {
				Ensure.NotNull(vNodeEndPoint, "vNodeEndPoint");
				Ensure.NotEmptyGuid(connectionId, "connectionId");

				VNodeEndPoint = vNodeEndPoint;
				ConnectionId = connectionId;
			}
		}

		[DerivedMessage]
		public partial class WaitForChaserToCatchUp : Message {
			public readonly Guid CorrelationId;
			public readonly TimeSpan TotalTimeWasted;

			public WaitForChaserToCatchUp(Guid correlationId, TimeSpan totalTimeWasted) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");

				CorrelationId = correlationId;
				TotalTimeWasted = totalTimeWasted;
			}
		}

		[DerivedMessage]
		public partial class ChaserCaughtUp : Message {
			public readonly Guid CorrelationId;

			public ChaserCaughtUp(Guid correlationId) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
			}
		}

		[DerivedMessage]
		public partial class EnablePreLeaderReplication : Message {
			public EnablePreLeaderReplication() {
			}
		}

		[DerivedMessage]
		public partial class CheckInaugurationConditions : Message {
			public CheckInaugurationConditions() {
			}
		}

		[DerivedMessage]
		public partial class RequestForwardingTimerTick : Message {
		}

		[DerivedMessage]
		public partial class NoQuorumMessage : Message {
		}

		[DerivedMessage]
		public partial class EpochWritten : Message {
			public readonly EpochRecord Epoch;

			public EpochWritten(EpochRecord epoch) {
				Ensure.NotNull(epoch, "epoch");
				Epoch = epoch;
			}
		}
	}
}
