using System;
using EventStore.Core.Cluster;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;
using EndPoint = System.Net.EndPoint;

namespace EventStore.Core.Messages {
	public static partial class SystemMessage {
		[StatsGroup("system")]
		public enum MessageType {
			None = 0,
			SystemInit = 1,
			SystemStart = 2,
			SystemCoreReady = 3,
			SystemReady = 4,
			ServiceInitialized = 5,
			SubSystemInitialized = 6,
			WriteEpoch = 7,
			InitiateLeaderResignation = 8,
			RequestQueueDrained = 9,
			BecomePreLeader = 10,
			BecomeLeader = 11,
			BecomeShuttingDown = 12,
			BecomeShutdown = 13,
			BecomeUnknown = 14,
			BecomeDiscoverLeader = 15,
			BecomeResigningLeader = 16,
			BecomePreReplica = 17,
			BecomeCatchingUp = 18,
			BecomeClone = 19,
			BecomeFollower = 20,
			BecomeReadOnlyLeaderless = 21,
			BecomePreReadOnlyReplica = 22,
			BecomeReadOnlyReplica = 23,
			ServiceShutdown = 24,
			ShutdownTimeout = 25,
			VNodeConnectionLost = 26,
			VNodeConnectionEstablished = 27,
			WaitForChaserToCatchUp = 28,
			ChaserCaughtUp = 29,
			EnablePreLeaderReplication = 30,
			CheckInaugurationConditions = 31,
			RequestForwardingTimerTick = 32,
			NoQuorumMessage = 33,
			EpochWritten = 34,
		}

		[StatsMessage(MessageType.SystemInit)]
		public partial class SystemInit : Message {
		}

		[StatsMessage(MessageType.SystemStart)]
		public partial class SystemStart : Message {
		}

		[StatsMessage(MessageType.SystemCoreReady)]
		public partial class SystemCoreReady : Message {
		}

		[StatsMessage(MessageType.SystemReady)]
		public partial class SystemReady : Message {
		}

		[StatsMessage(MessageType.ServiceInitialized)]
		public partial class ServiceInitialized : Message {
			public readonly string ServiceName;

			public ServiceInitialized(string serviceName) {
				Ensure.NotNullOrEmpty(serviceName, "serviceName");
				ServiceName = serviceName;
			}
		}

		[StatsMessage(MessageType.SubSystemInitialized)]
		public partial class SubSystemInitialized : Message {
			public readonly string SubSystemName;

			public SubSystemInitialized(string subSystemName) {
				Ensure.NotNullOrEmpty(subSystemName, "subSystemName");
				SubSystemName = subSystemName;
			}
		}

		[StatsMessage(MessageType.WriteEpoch)]
		public partial class WriteEpoch : Message {
			public readonly int EpochNumber;
			public WriteEpoch(int epochNumber) {
				EpochNumber = epochNumber;
			}
		}

		[StatsMessage(MessageType.InitiateLeaderResignation)]
		public partial class InitiateLeaderResignation : Message {
		}

		[StatsMessage(MessageType.RequestQueueDrained)]
		public partial class RequestQueueDrained : Message {
		}

		[StatsMessage]
		public abstract partial class StateChangeMessage : Message {
			public readonly Guid CorrelationId;
			public readonly VNodeState State;

			protected StateChangeMessage(Guid correlationId, VNodeState state) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				State = state;
			}
		}

		[StatsMessage(MessageType.BecomePreLeader)]
		public partial class BecomePreLeader : StateChangeMessage {
			public BecomePreLeader(Guid correlationId) : base(correlationId, VNodeState.PreLeader) {
			}
		}

		[StatsMessage(MessageType.BecomeLeader)]
		public partial class BecomeLeader : StateChangeMessage {
			public BecomeLeader(Guid correlationId) : base(correlationId, VNodeState.Leader) {
			}
		}

		[StatsMessage(MessageType.BecomeShuttingDown)]
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

		[StatsMessage(MessageType.BecomeShutdown)]
		public partial class BecomeShutdown : StateChangeMessage {
			public BecomeShutdown(Guid correlationId) : base(correlationId, VNodeState.Shutdown) {
			}
		}

		[StatsMessage(MessageType.BecomeUnknown)]
		public partial class BecomeUnknown : StateChangeMessage {
			public BecomeUnknown(Guid correlationId)
				: base(correlationId, VNodeState.Unknown) {
			}
		}

		[StatsMessage(MessageType.BecomeDiscoverLeader)]
		public partial class BecomeDiscoverLeader : StateChangeMessage {
			public BecomeDiscoverLeader(Guid correlationId)
				: base(correlationId, VNodeState.DiscoverLeader) {
			}
		}

		[StatsMessage(MessageType.BecomeResigningLeader)]
		public partial class BecomeResigningLeader : StateChangeMessage {
			public BecomeResigningLeader(Guid correlationId)
				: base(correlationId, VNodeState.ResigningLeader) {
			}
		}

		[StatsMessage]
		public abstract partial class ReplicaStateMessage : StateChangeMessage {
			public readonly MemberInfo Leader;

			protected ReplicaStateMessage(Guid correlationId, VNodeState state, MemberInfo leader)
				: base(correlationId, state) {
				Ensure.NotNull(leader, "leader");
				Leader = leader;
			}
		}

		[StatsMessage(MessageType.BecomePreReplica)]
		public partial class BecomePreReplica : ReplicaStateMessage {
			public readonly Guid LeaderConnectionCorrelationId;

			public BecomePreReplica(Guid correlationId, Guid leaderConnectionCorrelationId, MemberInfo leader)
				: base(correlationId, VNodeState.PreReplica, leader) {
				LeaderConnectionCorrelationId = leaderConnectionCorrelationId;
			}
		}

		[StatsMessage(MessageType.BecomeCatchingUp)]
		public partial class BecomeCatchingUp : ReplicaStateMessage {
			public BecomeCatchingUp(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.CatchingUp,
				leader) {
			}
		}

		[StatsMessage(MessageType.BecomeClone)]
		public partial class BecomeClone : ReplicaStateMessage {
			public BecomeClone(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.Clone, leader) {
			}
		}

		[StatsMessage(MessageType.BecomeFollower)]
		public partial class BecomeFollower : ReplicaStateMessage {
			public BecomeFollower(Guid correlationId, MemberInfo leader) : base(correlationId, VNodeState.Follower,
				leader) {
			}
		}

		[StatsMessage(MessageType.BecomeReadOnlyLeaderless)]
		public partial class BecomeReadOnlyLeaderless : StateChangeMessage {
			public BecomeReadOnlyLeaderless(Guid correlationId)
				: base(correlationId, VNodeState.ReadOnlyLeaderless) {
			}
		}

		[StatsMessage(MessageType.BecomePreReadOnlyReplica)]
		public partial class BecomePreReadOnlyReplica : ReplicaStateMessage {
			public readonly Guid LeaderConnectionCorrelationId;

			public BecomePreReadOnlyReplica(Guid correlationId, Guid leaderConnectionCorrelationId, MemberInfo leader)
				: base(correlationId, VNodeState.PreReadOnlyReplica, leader) {
				LeaderConnectionCorrelationId = leaderConnectionCorrelationId;
			}
		}

		[StatsMessage(MessageType.BecomeReadOnlyReplica)]
		public partial class BecomeReadOnlyReplica : ReplicaStateMessage {
			public BecomeReadOnlyReplica(Guid correlationId, MemberInfo leader)
				: base(correlationId, VNodeState.ReadOnlyReplica, leader) {
			}
		}


		[StatsMessage(MessageType.ServiceShutdown)]
		public partial class ServiceShutdown : Message {
			public readonly string ServiceName;

			public ServiceShutdown(string serviceName) {
				if (String.IsNullOrEmpty(serviceName))
					throw new ArgumentNullException("serviceName");
				ServiceName = serviceName;
			}
		}

		[StatsMessage(MessageType.ShutdownTimeout)]
		public partial class ShutdownTimeout : Message {
		}

		[StatsMessage(MessageType.VNodeConnectionLost)]
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

		[StatsMessage(MessageType.VNodeConnectionEstablished)]
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

		[StatsMessage(MessageType.WaitForChaserToCatchUp)]
		public partial class WaitForChaserToCatchUp : Message {
			public readonly Guid CorrelationId;
			public readonly TimeSpan TotalTimeWasted;

			public WaitForChaserToCatchUp(Guid correlationId, TimeSpan totalTimeWasted) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");

				CorrelationId = correlationId;
				TotalTimeWasted = totalTimeWasted;
			}
		}

		[StatsMessage(MessageType.ChaserCaughtUp)]
		public partial class ChaserCaughtUp : Message {
			public readonly Guid CorrelationId;

			public ChaserCaughtUp(Guid correlationId) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
			}
		}

		[StatsMessage(MessageType.EnablePreLeaderReplication)]
		public partial class EnablePreLeaderReplication : Message {
			public EnablePreLeaderReplication() {
			}
		}

		[StatsMessage(MessageType.CheckInaugurationConditions)]
		public partial class CheckInaugurationConditions : Message {
			public CheckInaugurationConditions() {
			}
		}

		[StatsMessage(MessageType.RequestForwardingTimerTick)]
		public partial class RequestForwardingTimerTick : Message {
		}

		[StatsMessage(MessageType.NoQuorumMessage)]
		public partial class NoQuorumMessage : Message {
		}

		[StatsMessage(MessageType.EpochWritten)]
		public partial class EpochWritten : Message {
			public readonly EpochRecord Epoch;

			public EpochWritten(EpochRecord epoch) {
				Ensure.NotNull(epoch, "epoch");
				Epoch = epoch;
			}
		}
	}
}
