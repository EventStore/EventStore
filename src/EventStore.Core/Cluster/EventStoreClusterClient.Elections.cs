using System;
using System.Threading.Tasks;
using EventStore.Cluster;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Grpc;
using EndPoint = System.Net.EndPoint;
using GossipEndPoint = EventStore.Cluster.EndPoint;

namespace EventStore.Core.Cluster {
	public partial class EventStoreClusterClient {
		public void SendViewChange(ElectionMessage.ViewChange msg, EndPoint destinationEndpoint, DateTime deadline) {
			SendViewChangeAsync(msg.ServerId, msg.ServerInternalHttp, msg.AttemptedView, deadline).ContinueWith(r => {
				if (r.Exception != null) {
					Log.Information(r.Exception, "View Change Send Failed to {Server}", destinationEndpoint);
				}
			});
		}

		public void SendViewChangeProof(ElectionMessage.ViewChangeProof msg, EndPoint destinationEndpoint,
			DateTime deadline) {
			SendViewChangeProofAsync(msg.ServerId, msg.ServerInternalHttp, msg.InstalledView, deadline).ContinueWith(
				r => {
					if (r.Exception != null) {
						Log.Information(r.Exception, "View Change Proof Send Failed to {Server}",
							destinationEndpoint);
					}
				});
		}

		public void SendPrepare(ElectionMessage.Prepare msg, EndPoint destinationEndpoint, DateTime deadline) {
			SendPrepareAsync(msg.ServerId, msg.ServerInternalHttp, msg.View, deadline).ContinueWith(r => {
				if (r.Exception != null) {
					Log.Information(r.Exception, "Prepare Send Failed to {Server}", destinationEndpoint);
				}
			});
		}

		public void SendPrepareOk(ElectionMessage.PrepareOk prepareOk, EndPoint destinationEndpoint,
			DateTime deadline) {
			SendPrepareOkAsync(prepareOk.View, prepareOk.ServerId, prepareOk.ServerInternalHttp, prepareOk.EpochNumber,
					prepareOk.EpochPosition, prepareOk.EpochId, prepareOk.LastCommitPosition,
					prepareOk.WriterCheckpoint,
					prepareOk.ChaserCheckpoint, prepareOk.NodePriority, deadline)
				.ContinueWith(r => {
					if (r.Exception != null) {
						Log.Information(r.Exception, "Prepare OK Send Failed to {Server}",
							destinationEndpoint);
					}
				});
		}

		public void SendProposal(ElectionMessage.Proposal proposal, EndPoint destinationEndpoint, DateTime deadline) {
			SendProposalAsync(proposal.ServerId, proposal.ServerInternalHttp, proposal.LeaderId,
					proposal.LeaderInternalHttp,
					proposal.View, proposal.EpochNumber, proposal.EpochPosition, proposal.EpochId,
					proposal.LastCommitPosition, proposal.WriterCheckpoint, proposal.ChaserCheckpoint,
					proposal.NodePriority,
					deadline)
				.ContinueWith(r => {
					if (r.Exception != null) {
						Log.Information(r.Exception, "Proposal Send Failed to {Server}",
							destinationEndpoint);
					}
				});
		}

		public void SendAccept(ElectionMessage.Accept accept, EndPoint destinationEndpoint, DateTime deadline) {
			SendAcceptAsync(accept.ServerId, accept.ServerInternalHttp, accept.LeaderId, accept.LeaderInternalHttp,
					accept.View, deadline)
				.ContinueWith(r => {
					if (r.Exception != null) {
						Log.Information(r.Exception, "Accept Send Failed to {Server}", destinationEndpoint);
					}
				});
		}

		public void SendLeaderIsResigning(ElectionMessage.LeaderIsResigning resigning, EndPoint destinationEndpoint,
			DateTime deadline) {
			SendLeaderIsResigningAsync(resigning.LeaderId, resigning.LeaderInternalHttp, deadline).ContinueWith(r => {
				if (r.Exception != null) {
					Log.Information(r.Exception, "Leader is Resigning Send Failed to {Server}", destinationEndpoint);
				}
			});
		}

		public void SendLeaderIsResigningOk(ElectionMessage.LeaderIsResigningOk resigningOk,
			EndPoint destinationEndpoint, DateTime deadline) {
			SendLeaderIsResigningOkAsync(resigningOk.LeaderId, resigningOk.LeaderInternalHttp,
				resigningOk.ServerId, resigningOk.ServerInternalHttp, deadline).ContinueWith(r => {
				if (r.Exception != null) {
					Log.Information(r.Exception, "Leader is Resigning Ok Send Failed to {Server}", destinationEndpoint);
				}
			});
		}

		private async Task SendViewChangeAsync(Guid serverId, EndPoint serverInternalHttp, int attemptedView,
			DateTime deadline) {
			var request = new ViewChangeRequest {
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new GossipEndPoint(serverInternalHttp.GetHost(), (uint)serverInternalHttp.GetPort()),
				AttemptedView = attemptedView
			};
			await _electionsClient.ViewChangeAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendViewChangeProofAsync(Guid serverId, EndPoint serverInternalHttp, int installedView,
			DateTime deadline) {
			var request = new ViewChangeProofRequest {
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new GossipEndPoint(serverInternalHttp.GetHost(), (uint)serverInternalHttp.GetPort()),
				InstalledView = installedView
			};
			await _electionsClient.ViewChangeProofAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendPrepareAsync(Guid serverId, EndPoint serverInternalHttp, int view, DateTime deadline) {
			var request = new PrepareRequest {
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new GossipEndPoint(serverInternalHttp.GetHost(), (uint)serverInternalHttp.GetPort()),
				View = view
			};
			await _electionsClient.PrepareAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendPrepareOkAsync(int view, Guid serverId, EndPoint serverInternalHttp, int epochNumber,
			long epochPosition, Guid epochId, long lastCommitPosition, long writerCheckpoint, long chaserCheckpoint,
			int nodePriority, DateTime deadline) {
			var request = new PrepareOkRequest {
				View = view,
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new GossipEndPoint(serverInternalHttp.GetHost(), (uint)serverInternalHttp.GetPort()),
				EpochNumber = epochNumber,
				EpochPosition = epochPosition,
				EpochId = Uuid.FromGuid(epochId).ToDto(),
				LastCommitPosition = lastCommitPosition,
				WriterCheckpoint = writerCheckpoint,
				ChaserCheckpoint = chaserCheckpoint,
				NodePriority = nodePriority
			};
			await _electionsClient.PrepareOkAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendProposalAsync(Guid serverId, EndPoint serverInternalHttp, Guid leaderId,
			EndPoint leaderInternalHttp, int view, int epochNumber, long epochPosition, Guid epochId,
			long lastCommitPosition, long writerCheckpoint, long chaserCheckpoint, int nodePriority,
			DateTime deadline) {
			var request = new ProposalRequest {
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new GossipEndPoint(serverInternalHttp.GetHost(), (uint)serverInternalHttp.GetPort()),
				LeaderId = Uuid.FromGuid(leaderId).ToDto(),
				LeaderInternalHttp = new GossipEndPoint(leaderInternalHttp.GetHost(), (uint)leaderInternalHttp.GetPort()),
				View = view,
				EpochNumber = epochNumber,
				EpochPosition = epochPosition,
				EpochId = Uuid.FromGuid(epochId).ToDto(),
				LastCommitPosition = lastCommitPosition,
				WriterCheckpoint = writerCheckpoint,
				ChaserCheckpoint = chaserCheckpoint,
				NodePriority = nodePriority
			};
			await _electionsClient.ProposalAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendAcceptAsync(Guid serverId, EndPoint serverInternalHttp, Guid leaderId,
			EndPoint leaderInternalHttp, int view, DateTime deadline) {
			var request = new AcceptRequest {
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new GossipEndPoint(serverInternalHttp.GetHost(), (uint)serverInternalHttp.GetPort()),
				LeaderId = Uuid.FromGuid(leaderId).ToDto(),
				LeaderInternalHttp = new GossipEndPoint(leaderInternalHttp.GetHost(), (uint)leaderInternalHttp.GetPort()),
				View = view
			};
			await _electionsClient.AcceptAsync(request);
			_electionsClient.Accept(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendLeaderIsResigningAsync(Guid leaderId, EndPoint leaderInternalHttp, DateTime deadline) {
			var request = new LeaderIsResigningRequest {
				LeaderId = Uuid.FromGuid(leaderId).ToDto(),
				LeaderInternalHttp = new GossipEndPoint(leaderInternalHttp.GetHost(), (uint)leaderInternalHttp.GetPort()),
			};
			await _electionsClient.LeaderIsResigningAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendLeaderIsResigningOkAsync(Guid leaderId, EndPoint leaderInternalHttp,
			Guid serverId, EndPoint serverInternalHttp, DateTime deadline) {
			var request = new LeaderIsResigningOkRequest {
				LeaderId = Uuid.FromGuid(leaderId).ToDto(),
				LeaderInternalHttp = new GossipEndPoint(leaderInternalHttp.GetHost(), (uint)leaderInternalHttp.GetPort()),
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new GossipEndPoint(serverInternalHttp.GetHost(), (uint)serverInternalHttp.GetPort()),
			};
			await _electionsClient.LeaderIsResigningOkAsync(request, deadline: deadline.ToUniversalTime());
		}
	}
}
