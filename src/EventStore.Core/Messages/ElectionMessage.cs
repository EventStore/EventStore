using System;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static class ElectionMessage {
		public class StartElections : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public override string ToString() {
				return "---- StartElections";
			}
		}

		public class ViewChange : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid ServerId;
			public readonly EndPoint ServerHttpEndPoint;

			public readonly int AttemptedView;

			public ViewChange(Guid serverId,
				EndPoint serverHttpEndPoint,
				int attemptedView) {
				ServerId = serverId;
				ServerHttpEndPoint = serverHttpEndPoint;

				AttemptedView = attemptedView;
			}

			public ViewChange(ElectionMessageDto.ViewChangeDto dto) {
				AttemptedView = dto.AttemptedView;
				ServerId = dto.ServerId;
				ServerHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.ServerHttpAddress),
					dto.ServerHttpPort);
			}

			public override string ToString() {
				return string.Format("---- ViewChange: attemptedView {0}, serverId {1}, serverHttp {2}",
					AttemptedView, ServerId, ServerHttpEndPoint);
			}
		}

		public class ViewChangeProof : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid ServerId;
			public readonly EndPoint ServerHttpEndPoint;
			public readonly int InstalledView;

			public ViewChangeProof(Guid serverId, EndPoint serverHttpEndPoint, int installedView) {
				ServerId = serverId;
				ServerHttpEndPoint = serverHttpEndPoint;
				InstalledView = installedView;
			}

			public ViewChangeProof(ElectionMessageDto.ViewChangeProofDto dto) {
				ServerId = dto.ServerId;
				ServerHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.ServerHttpAddress),
					dto.ServerHttpPort);
				InstalledView = dto.InstalledView;
			}

			public override string ToString() {
				return string.Format("---- ViewChangeProof: serverId {0}, serverHttp {1}, installedView {2}",
					ServerId, ServerHttpEndPoint, InstalledView);
			}
		}

		public class SendViewChangeProof : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public override string ToString() {
				return string.Format("---- SendViewChangeProof");
			}
		}

		public class ElectionsTimedOut : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int View;

			public ElectionsTimedOut(int view) {
				View = view;
			}

			public override string ToString() {
				return string.Format("---- ElectionsTimedOut: view {0}", View);
			}
		}

		public class Prepare : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid ServerId;
			public readonly EndPoint ServerHttpEndPoint;
			public readonly int View;

			public Prepare(Guid serverId, EndPoint serverHttpEndPoint, int view) {
				ServerId = serverId;
				ServerHttpEndPoint = serverHttpEndPoint;
				View = view;
			}

			public Prepare(ElectionMessageDto.PrepareDto dto) {
				ServerId = dto.ServerId;
				ServerHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.ServerHttpAddress),
					dto.ServerHttpPort);
				View = dto.View;
			}

			public override string ToString() {
				return string.Format("---- Prepare: serverId {0}, serverHttp {1}, view {2}", ServerId,
					ServerHttpEndPoint, View);
			}
		}

		public class PrepareOk : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int View;
			public readonly Guid ServerId;
			public readonly EndPoint ServerHttpEndPoint;
			public readonly int EpochNumber;
			public readonly long EpochPosition;
			public readonly Guid EpochId;
			public readonly Guid EpochLeaderInstanceId;
			public readonly long LastCommitPosition;
			public readonly long WriterCheckpoint;
			public readonly long ChaserCheckpoint;
			public readonly long ReplicationCheckpoint;
			public readonly int NodePriority;
			public readonly ClusterInfo ClusterInfo;

			public PrepareOk(int view,
				Guid serverId,
				EndPoint serverHttpEndPoint,
				int epochNumber,
				long epochPosition,
				Guid epochId,
				Guid epochLeaderInstanceId,
				long lastCommitPosition,
				long writerCheckpoint,
				long chaserCheckpoint,
				long replicationCheckpoint,
				int nodePriority,
				ClusterInfo clusterInfo) {
				View = view;
				ServerId = serverId;
				ServerHttpEndPoint = serverHttpEndPoint;
				EpochNumber = epochNumber;
				EpochPosition = epochPosition;
				EpochId = epochId;
				EpochLeaderInstanceId = epochLeaderInstanceId;
				LastCommitPosition = lastCommitPosition;
				WriterCheckpoint = writerCheckpoint;
				ChaserCheckpoint = chaserCheckpoint;
				ReplicationCheckpoint = replicationCheckpoint;
				NodePriority = nodePriority;
				ClusterInfo = clusterInfo;
			}

			public PrepareOk(ElectionMessageDto.PrepareOkDto dto) {
				View = dto.View;
				ServerId = dto.ServerId;
				ServerHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.ServerHttpAddress),
					dto.ServerHttpPort);
				EpochNumber = dto.EpochNumber;
				EpochPosition = dto.EpochPosition;
				EpochId = dto.EpochId;
				EpochLeaderInstanceId = dto.EpochLeaderInstanceId;
				LastCommitPosition = dto.LastCommitPosition;
				WriterCheckpoint = dto.WriterCheckpoint;
				ChaserCheckpoint = dto.ChaserCheckpoint;
				ReplicationCheckpoint = dto.ReplicationCheckpoint;
				NodePriority = dto.NodePriority;
				ClusterInfo = dto.ClusterInfo;
			}

			public override string ToString() {
				return string.Format(
					"---- PrepareOk: view {0}, serverId {1}, serverHttp {2}, epochNumber {3}, " +
					"epochPosition {4}, epochId {5}, epochLeaderInstanceId {6:B}, lastCommitPosition {7}, writerCheckpoint {8}, chaserCheckpoint {9}, replicationCheckpoint {10}, nodePriority: {11}, clusterInfo: {12}",
					View, ServerId, ServerHttpEndPoint, EpochNumber,
					EpochPosition, EpochId, EpochLeaderInstanceId, LastCommitPosition, WriterCheckpoint, ChaserCheckpoint, ReplicationCheckpoint, NodePriority, ClusterInfo);
			}
		}

		public class Proposal : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid ServerId;
			public readonly EndPoint ServerHttpEndPoint;
			public readonly Guid LeaderId;
			public readonly EndPoint LeaderHttpEndPoint;

			public readonly int View;
			public readonly int EpochNumber;
			public readonly long EpochPosition;
			public readonly Guid EpochId;
			public readonly Guid EpochLeaderInstanceId;
			public readonly long LastCommitPosition;
			public readonly long WriterCheckpoint;
			public readonly long ChaserCheckpoint;
			public readonly long ReplicationCheckpoint;
			public readonly int NodePriority;

			public Proposal(Guid serverId, EndPoint serverHttpEndPoint, Guid leaderId, EndPoint leaderHttpEndPoint,
				int view, int epochNumber, long epochPosition, Guid epochId, Guid epochLeaderInstanceId,
				long lastCommitPosition, long writerCheckpoint, long chaserCheckpoint, long replicationCheckpoint, int nodePriority) {
				ServerId = serverId;
				ServerHttpEndPoint = serverHttpEndPoint;
				LeaderId = leaderId;
				LeaderHttpEndPoint = leaderHttpEndPoint;
				View = view;
				EpochNumber = epochNumber;
				EpochPosition = epochPosition;
				EpochId = epochId;
				EpochLeaderInstanceId = epochLeaderInstanceId;
				LastCommitPosition = lastCommitPosition;
				WriterCheckpoint = writerCheckpoint;
				ChaserCheckpoint = chaserCheckpoint;
				ReplicationCheckpoint = replicationCheckpoint;
				NodePriority = nodePriority;
			}

			public Proposal(ElectionMessageDto.ProposalDto dto) {
				ServerId = dto.ServerId;
				ServerHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.ServerHttpAddress),
					dto.ServerHttpPort);
				LeaderId = dto.LeaderId;
				LeaderHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.LeaderHttpAddress),
					dto.LeaderHttpPort);
				View = dto.View;
				EpochNumber = dto.EpochNumber;
				EpochPosition = dto.EpochPosition;
				EpochId = dto.EpochId;
				EpochLeaderInstanceId = dto.EpochLeaderInstanceId;
				LastCommitPosition = dto.LastCommitPosition;
				WriterCheckpoint = dto.WriterCheckpoint;
				ChaserCheckpoint = dto.ChaserCheckpoint;
				ReplicationCheckpoint = dto.ReplicationCheckpoint;
				NodePriority = dto.NodePriority;
			}

			public override string ToString() {
				return string.Format(
					"---- Proposal: serverId {0}, serverHttp {1}, leaderId {2}, leaderHttp {3}, "
					+ "view {4}, lastCommitCheckpoint {5}, writerCheckpoint {6}, chaserCheckpoint {7}, replicationCheckpoint {8}, epoch {9}@{10}:{11:B} (L={12:B}), NodePriority {13}",
					ServerId, ServerHttpEndPoint, LeaderId, LeaderHttpEndPoint,
					View, LastCommitPosition, WriterCheckpoint, ChaserCheckpoint, ReplicationCheckpoint,
					EpochNumber, EpochPosition, EpochId, EpochLeaderInstanceId, NodePriority);
			}
		}

		public class Accept : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid ServerId;
			public readonly EndPoint ServerHttpEndPoint;
			public readonly Guid LeaderId;
			public readonly EndPoint LeaderHttpEndPoint;
			public readonly int View;

			public Accept(Guid serverId, EndPoint serverHttpEndPoint, Guid leaderId, EndPoint leaderHttpEndPoint,
				int view) {
				ServerId = serverId;
				ServerHttpEndPoint = serverHttpEndPoint;
				LeaderId = leaderId;
				LeaderHttpEndPoint = leaderHttpEndPoint;

				View = view;
			}

			public Accept(ElectionMessageDto.AcceptDto dto) {
				ServerId = dto.ServerId;
				ServerHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.ServerHttpAddress),
					dto.ServerHttpPort);
				LeaderId = dto.LeaderId;
				LeaderHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.LeaderHttpAddress),
					dto.LeaderHttpPort);
				View = dto.View;
			}

			public override string ToString() {
				return string.Format(
					"---- Accept: serverId {0}, serverHttp {1}, leaderId {2}, leaderHttp {3}, view {4}",
					ServerId, ServerHttpEndPoint, LeaderId, LeaderHttpEndPoint, View);
			}
		}
		
		public class LeaderIsResigning : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid LeaderId;
			public readonly EndPoint LeaderHttpEndPoint;

			public LeaderIsResigning(Guid leaderId, EndPoint leaderHttpEndPoint) {
				LeaderId = leaderId;
				LeaderHttpEndPoint = leaderHttpEndPoint;
			}

			public LeaderIsResigning(ElectionMessageDto.LeaderIsResigningDto dto) {
				LeaderId = dto.LeaderId;
				LeaderHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.LeaderHttpAddress),
					dto.LeaderHttpPort);
			}

			public override string ToString() {
				return $"---- LeaderIsResigning: serverId {LeaderId}";
			}
		}
		
		public class LeaderIsResigningOk : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid LeaderId;
			public readonly EndPoint LeaderHttpEndPoint;
			public readonly Guid ServerId;
			public readonly EndPoint ServerHttpEndPoint;
			
			public LeaderIsResigningOk(ElectionMessageDto.LeaderIsResigningOkDto dto) {
				LeaderId = dto.LeaderId;
				LeaderHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.LeaderHttpAddress),
					dto.LeaderHttpPort);
				ServerId = dto.ServerId;
				ServerHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.ServerHttpAddress),
					dto.ServerHttpPort);
			}

			public LeaderIsResigningOk(Guid leaderId, EndPoint leaderHttpEndPoint, Guid serverId, EndPoint serverHttpEndPoint) {
				LeaderId = leaderId;
				LeaderHttpEndPoint = leaderHttpEndPoint;
				ServerId = serverId;
				ServerHttpEndPoint = serverHttpEndPoint;
			}

			public override string ToString() {
				return $"---- LeaderIsResigningOk: serverId {ServerId}";
			}
		}

		public class ElectionsDone : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int InstalledView;
			public readonly MemberInfo Leader;

			public ElectionsDone(int installedView, MemberInfo leader) {
				Ensure.Nonnegative(installedView, "installedView");
				Ensure.NotNull(leader, "leader");
				InstalledView = installedView;
				Leader = leader;
			}

			public override string ToString() {
				return string.Format("---- ElectionsDone: installedView {0}, leader {1}", InstalledView, Leader);
			}
		}
	}
}
