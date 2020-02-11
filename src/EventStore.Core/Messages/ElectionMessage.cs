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
			public readonly IPEndPoint ServerInternalHttp;

			public readonly int AttemptedView;

			public ViewChange(Guid serverId,
				IPEndPoint serverInternalHttp,
				int attemptedView) {
				ServerId = serverId;
				ServerInternalHttp = serverInternalHttp;

				AttemptedView = attemptedView;
			}

			public ViewChange(ElectionMessageDto.ViewChangeDto dto) {
				AttemptedView = dto.AttemptedView;
				ServerId = dto.ServerId;
				ServerInternalHttp = new IPEndPoint(IPAddress.Parse(dto.ServerInternalHttpAddress),
					dto.ServerInternalHttpPort);
			}

			public override string ToString() {
				return string.Format("---- ViewChange: attemptedView {0}, serverId {1}, serverInternalHttp {2}",
					AttemptedView, ServerId, ServerInternalHttp);
			}
		}

		public class ViewChangeProof : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid ServerId;
			public readonly IPEndPoint ServerInternalHttp;
			public readonly int InstalledView;

			public ViewChangeProof(Guid serverId, IPEndPoint serverInternalHttp, int installedView) {
				ServerId = serverId;
				ServerInternalHttp = serverInternalHttp;
				InstalledView = installedView;
			}

			public ViewChangeProof(ElectionMessageDto.ViewChangeProofDto dto) {
				ServerId = dto.ServerId;
				ServerInternalHttp = new IPEndPoint(IPAddress.Parse(dto.ServerInternalHttpAddress),
					dto.ServerInternalHttpPort);
				InstalledView = dto.InstalledView;
			}

			public override string ToString() {
				return string.Format("---- ViewChangeProof: serverId {0}, serverInternalHttp {1}, installedView {2}",
					ServerId, ServerInternalHttp, InstalledView);
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
			public readonly IPEndPoint ServerInternalHttp;
			public readonly int View;

			public Prepare(Guid serverId, IPEndPoint serverInternalHttp, int view) {
				ServerId = serverId;
				ServerInternalHttp = serverInternalHttp;
				View = view;
			}

			public Prepare(ElectionMessageDto.PrepareDto dto) {
				ServerId = dto.ServerId;
				ServerInternalHttp = new IPEndPoint(IPAddress.Parse(dto.ServerInternalHttpAddress),
					dto.ServerInternalHttpPort);
				View = dto.View;
			}

			public override string ToString() {
				return string.Format("---- Prepare: serverId {0}, serverInternalHttp {1}, view {2}", ServerId,
					ServerInternalHttp, View);
			}
		}

		public class PrepareOk : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int View;
			public readonly Guid ServerId;
			public readonly IPEndPoint ServerInternalHttp;
			public readonly int EpochNumber;
			public readonly long EpochPosition;
			public readonly Guid EpochId;
			public readonly long LastCommitPosition;
			public readonly long WriterCheckpoint;
			public readonly long ChaserCheckpoint;
			public readonly int NodePriority;

			public PrepareOk(int view,
				Guid serverId,
				IPEndPoint serverInternalHttp,
				int epochNumber,
				long epochPosition,
				Guid epochId,
				long lastCommitPosition,
				long writerCheckpoint,
				long chaserCheckpoint,
				int nodePriority) {
				View = view;
				ServerId = serverId;
				ServerInternalHttp = serverInternalHttp;
				EpochNumber = epochNumber;
				EpochPosition = epochPosition;
				EpochId = epochId;
				LastCommitPosition = lastCommitPosition;
				WriterCheckpoint = writerCheckpoint;
				ChaserCheckpoint = chaserCheckpoint;
				NodePriority = nodePriority;
			}

			public PrepareOk(ElectionMessageDto.PrepareOkDto dto) {
				View = dto.View;
				ServerId = dto.ServerId;
				ServerInternalHttp = new IPEndPoint(IPAddress.Parse(dto.ServerInternalHttpAddress),
					dto.ServerInternalHttpPort);
				EpochNumber = dto.EpochNumber;
				EpochPosition = dto.EpochPosition;
				EpochId = dto.EpochId;
				LastCommitPosition = dto.LastCommitPosition;
				WriterCheckpoint = dto.WriterCheckpoint;
				ChaserCheckpoint = dto.ChaserCheckpoint;
				NodePriority = dto.NodePriority;
			}

			public override string ToString() {
				return string.Format(
					"---- PrepareOk: view {0}, serverId {1}, serverInternalHttp {2}, epochNumber {3}, " +
					"epochPosition {4}, epochId {5}, lastCommitPosition {6}, writerCheckpoint {7}, chaserCheckpoint {8}, nodePriority: {9}",
					View, ServerId, ServerInternalHttp, EpochNumber,
					EpochPosition, EpochId, LastCommitPosition, WriterCheckpoint, ChaserCheckpoint, NodePriority);
			}
		}

		public class Proposal : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid ServerId;
			public readonly IPEndPoint ServerInternalHttp;
			public readonly Guid LeaderId;
			public readonly IPEndPoint LeaderInternalHttp;

			public readonly int View;
			public readonly int EpochNumber;
			public readonly long EpochPosition;
			public readonly Guid EpochId;
			public readonly long LastCommitPosition;
			public readonly long WriterCheckpoint;
			public readonly long ChaserCheckpoint;
			public readonly int NodePriority;

			public Proposal(Guid serverId, IPEndPoint serverInternalHttp, Guid leaderId, IPEndPoint leaderInternalHttp,
				int view, int epochNumber, long epochPosition, Guid epochId,
				long lastCommitPosition, long writerCheckpoint, long chaserCheckpoint, int nodePriority) {
				ServerId = serverId;
				ServerInternalHttp = serverInternalHttp;
				LeaderId = leaderId;
				LeaderInternalHttp = leaderInternalHttp;
				View = view;
				EpochNumber = epochNumber;
				EpochPosition = epochPosition;
				EpochId = epochId;
				LastCommitPosition = lastCommitPosition;
				WriterCheckpoint = writerCheckpoint;
				ChaserCheckpoint = chaserCheckpoint;
				NodePriority = nodePriority;
			}

			public Proposal(ElectionMessageDto.ProposalDto dto) {
				ServerId = dto.ServerId;
				ServerInternalHttp = new IPEndPoint(IPAddress.Parse(dto.ServerInternalHttpAddress),
					dto.ServerInternalHttpPort);
				LeaderId = dto.LeaderId;
				LeaderInternalHttp = new IPEndPoint(IPAddress.Parse(dto.LeaderInternalHttpAddress),
					dto.LeaderInternalHttpPort);
				View = dto.View;
				EpochNumber = dto.EpochNumber;
				EpochPosition = dto.EpochPosition;
				EpochId = dto.EpochId;
				LastCommitPosition = dto.LastCommitPosition;
				WriterCheckpoint = dto.WriterCheckpoint;
				ChaserCheckpoint = dto.ChaserCheckpoint;
				NodePriority = dto.NodePriority;
			}

			public override string ToString() {
				return string.Format(
					"---- Proposal: serverId {0}, serverInternalHttp {1}, leaderId {2}, leaderInternalHttp {3}, "
					+ "view {4}, lastCommitCheckpoint {5}, writerCheckpoint {6}, chaserCheckpoint {7}, epoch {8}@{9}:{10:B}, NodePriority {11}",
					ServerId, ServerInternalHttp, LeaderId, LeaderInternalHttp,
					View, LastCommitPosition, WriterCheckpoint, ChaserCheckpoint,
					EpochNumber, EpochPosition, EpochId, NodePriority);
			}
		}

		public class Accept : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid ServerId;
			public readonly IPEndPoint ServerInternalHttp;
			public readonly Guid LeaderId;
			public readonly IPEndPoint LeaderInternalHttp;
			public readonly int View;

			public Accept(Guid serverId, IPEndPoint serverInternalHttp, Guid leaderId, IPEndPoint leaderInternalHttp,
				int view) {
				ServerId = serverId;
				ServerInternalHttp = serverInternalHttp;
				LeaderId = leaderId;
				LeaderInternalHttp = leaderInternalHttp;

				View = view;
			}

			public Accept(ElectionMessageDto.AcceptDto dto) {
				ServerId = dto.ServerId;
				ServerInternalHttp = new IPEndPoint(IPAddress.Parse(dto.ServerInternalHttpAddress),
					dto.ServerInternalHttpPort);
				LeaderId = dto.LeaderId;
				LeaderInternalHttp = new IPEndPoint(IPAddress.Parse(dto.LeaderInternalHttpAddress),
					dto.LeaderInternalHttpPort);
				View = dto.View;
			}

			public override string ToString() {
				return string.Format(
					"---- Accept: serverId {0}, serverInternalHttp {1}, leaderId {2}, leaderInternalHttp {3}, view {4}",
					ServerId, ServerInternalHttp, LeaderId, LeaderInternalHttp, View);
			}
		}
		
		public class LeaderIsResigning : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid LeaderId;
			public readonly IPEndPoint LeaderInternalHttp;

			public LeaderIsResigning(Guid leaderId, IPEndPoint leaderInternalHttp) {
				LeaderId = leaderId;
				LeaderInternalHttp = leaderInternalHttp;
			}

			public LeaderIsResigning(ElectionMessageDto.LeaderIsResigningDto dto) {
				LeaderId = dto.LeaderId;
				LeaderInternalHttp = new IPEndPoint(IPAddress.Parse(dto.LeaderInternalHttpAddress),
					dto.LeaderInternalHttpPort);
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
			public readonly IPEndPoint LeaderInternalHttp;
			public readonly Guid ServerId;
			public readonly IPEndPoint ServerInternalHttp;
			
			public LeaderIsResigningOk(ElectionMessageDto.LeaderIsResigningOkDto dto) {
				LeaderId = dto.LeaderId;
				LeaderInternalHttp = new IPEndPoint(IPAddress.Parse(dto.LeaderInternalHttpAddress),
					dto.LeaderInternalHttpPort);
				ServerId = dto.ServerId;
				ServerInternalHttp = new IPEndPoint(IPAddress.Parse(dto.ServerInternalHttpAddress),
					dto.ServerInternalHttpPort);
			}

			public LeaderIsResigningOk(Guid leaderId, IPEndPoint leaderInternalHttp, Guid serverId, IPEndPoint serverInternalHttp) {
				LeaderId = leaderId;
				LeaderInternalHttp = leaderInternalHttp;
				ServerId = serverId;
				ServerInternalHttp = serverInternalHttp;
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
