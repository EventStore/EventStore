using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Cluster;
using EventStore.Core.Messages;
using EndPoint = EventStore.Cluster.EndPoint;

namespace EventStore.Core.Cluster {
	public partial class EventStoreClusterClient {
		public void SendViewChange(ElectionMessage.ViewChange msg, IPEndPoint destinationEndpoint, DateTime deadline) {
			SendViewChangeAsync(msg.ServerId, msg.ServerInternalHttp, msg.AttemptedView, deadline).ContinueWith(r => {
				if (r.Exception != null) {
					Log.Error(r.Exception, "View Change Send Failed to {Server}", destinationEndpoint);
				}
			});
		}

		public void SendViewChangeProof(ElectionMessage.ViewChangeProof msg, IPEndPoint destinationEndpoint,
			DateTime deadline) {
			SendViewChangeProofAsync(msg.ServerId, msg.ServerInternalHttp, msg.InstalledView, deadline).ContinueWith(
				r => {
					if (r.Exception != null) {
						Log.Error(r.Exception, "View Change Proof Send Failed to {Server}",
							destinationEndpoint);
					}
				});
		}

		public void SendPrepare(ElectionMessage.Prepare msg, IPEndPoint destinationEndpoint, DateTime deadline) {
			SendPrepareAsync(msg.ServerId, msg.ServerInternalHttp, msg.View, deadline).ContinueWith(r => {
				if (r.Exception != null) {
					Log.Error(r.Exception, "Prepare Send Failed to {Server}", destinationEndpoint);
				}
			});
		}

		public void SendPrepareOk(ElectionMessage.PrepareOk prepareOk, IPEndPoint destinationEndpoint,
			DateTime deadline) {
			SendPrepareOkAsync(prepareOk.View, prepareOk.ServerId, prepareOk.ServerInternalHttp, prepareOk.EpochNumber,
					prepareOk.EpochPosition, prepareOk.EpochId, prepareOk.LastCommitPosition,
					prepareOk.WriterCheckpoint,
					prepareOk.ChaserCheckpoint, prepareOk.NodePriority, deadline)
				.ContinueWith(r => {
					if (r.Exception != null) {
						Log.Error(r.Exception, "Prepare OK Send Failed to {Server}",
							destinationEndpoint);
					}
				});
		}

		public void SendProposal(ElectionMessage.Proposal proposal, IPEndPoint destinationEndpoint, DateTime deadline) {
			SendProposalAsync(proposal.ServerId, proposal.ServerInternalHttp, proposal.LeaderId,
					proposal.LeaderInternalHttp,
					proposal.View, proposal.EpochNumber, proposal.EpochPosition, proposal.EpochId,
					proposal.LastCommitPosition, proposal.WriterCheckpoint, proposal.ChaserCheckpoint,
					proposal.NodePriority,
					deadline)
				.ContinueWith(r => {
					if (r.Exception != null) {
						Log.Error(r.Exception, "Proposal Send Failed to {Server}",
							destinationEndpoint);
					}
				});
		}

		public void SendAccept(ElectionMessage.Accept accept, IPEndPoint destinationEndpoint, DateTime deadline) {
			SendAcceptAsync(accept.ServerId, accept.ServerInternalHttp, accept.LeaderId, accept.LeaderInternalHttp,
					accept.View, deadline)
				.ContinueWith(r => {
					if (r.Exception != null) {
						Log.Error(r.Exception, "Accept Send Failed to {Server}", destinationEndpoint);
					}
				});
		}

		public void SendLeaderIsResigning(ElectionMessage.LeaderIsResigning resigning, IPEndPoint destinationEndpoint,
			DateTime deadline) {
			SendLeaderIsResigningAsync(resigning.LeaderId, resigning.LeaderInternalHttp, deadline).ContinueWith(r => {
				if (r.Exception != null) {
					Log.Error(r.Exception, "Leader is Resigning Send Failed to {Server}", destinationEndpoint);
				}
			});
		}

		public void SendLeaderIsResigningOk(ElectionMessage.LeaderIsResigningOk resigningOk,
			IPEndPoint destinationEndpoint, DateTime deadline) {
			SendLeaderIsResigningOkAsync(resigningOk.LeaderId, resigningOk.LeaderInternalHttp,
				resigningOk.ServerId, resigningOk.ServerInternalHttp, deadline).ContinueWith(r => {
				if (r.Exception != null) {
					Log.Error(r.Exception, "Leader is Resigning Ok Send Failed to {Server}", destinationEndpoint);
				}
			});
		}

		private async Task SendViewChangeAsync(Guid serverId, IPEndPoint serverInternalHttp, int attemptedView,
			DateTime deadline) {
			var request = new ViewChangeRequest {
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new EndPoint(serverInternalHttp.Address.ToString(), (uint)serverInternalHttp.Port),
				AttemptedView = attemptedView
			};
			await _client.ViewChangeAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendViewChangeProofAsync(Guid serverId, IPEndPoint serverInternalHttp, int installedView,
			DateTime deadline) {
			var request = new ViewChangeProofRequest {
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new EndPoint(serverInternalHttp.Address.ToString(), (uint)serverInternalHttp.Port),
				InstalledView = installedView
			};
			await _client.ViewChangeProofAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendPrepareAsync(Guid serverId, IPEndPoint serverInternalHttp, int view, DateTime deadline) {
			var request = new PrepareRequest {
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new EndPoint(serverInternalHttp.Address.ToString(), (uint)serverInternalHttp.Port),
				View = view
			};
			await _client.PrepareAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendPrepareOkAsync(int view, Guid serverId, IPEndPoint serverInternalHttp, int epochNumber,
			long epochPosition, Guid epochId, long lastCommitPosition, long writerCheckpoint, long chaserCheckpoint,
			int nodePriority, DateTime deadline) {
			var request = new PrepareOkRequest {
				View = view,
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new EndPoint(serverInternalHttp.Address.ToString(), (uint)serverInternalHttp.Port),
				EpochNumber = epochNumber,
				EpochPosition = epochPosition,
				EpochId = Uuid.FromGuid(epochId).ToDto(),
				LastCommitPosition = lastCommitPosition,
				WriterCheckpoint = writerCheckpoint,
				ChaserCheckpoint = chaserCheckpoint,
				NodePriority = nodePriority
			};
			await _client.PrepareOkAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendProposalAsync(Guid serverId, IPEndPoint serverInternalHttp, Guid leaderId,
			IPEndPoint leaderInternalHttp, int view, int epochNumber, long epochPosition, Guid epochId,
			long lastCommitPosition, long writerCheckpoint, long chaserCheckpoint, int nodePriority,
			DateTime deadline) {
			var request = new ProposalRequest {
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new EndPoint(serverInternalHttp.Address.ToString(), (uint)serverInternalHttp.Port),
				LeaderId = Uuid.FromGuid(leaderId).ToDto(),
				LeaderInternalHttp = new EndPoint(leaderInternalHttp.Address.ToString(), (uint)leaderInternalHttp.Port),
				View = view,
				EpochNumber = epochNumber,
				EpochPosition = epochPosition,
				EpochId = Uuid.FromGuid(epochId).ToDto(),
				LastCommitPosition = lastCommitPosition,
				WriterCheckpoint = writerCheckpoint,
				ChaserCheckpoint = chaserCheckpoint,
				NodePriority = nodePriority
			};
			await _client.ProposalAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendAcceptAsync(Guid serverId, IPEndPoint serverInternalHttp, Guid leaderId,
			IPEndPoint leaderInternalHttp, int view, DateTime deadline) {
			var request = new AcceptRequest {
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new EndPoint(serverInternalHttp.Address.ToString(), (uint)serverInternalHttp.Port),
				LeaderId = Uuid.FromGuid(leaderId).ToDto(),
				LeaderInternalHttp = new EndPoint(leaderInternalHttp.Address.ToString(), (uint)leaderInternalHttp.Port),
				View = view
			};
			await _client.AcceptAsync(request);
			_client.Accept(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendLeaderIsResigningAsync(Guid leaderId, IPEndPoint leaderInternalHttp, DateTime deadline) {
			var request = new LeaderIsResigningRequest {
				LeaderId = Uuid.FromGuid(leaderId).ToDto(),
				LeaderInternalHttp = new EndPoint(leaderInternalHttp.Address.ToString(), (uint)leaderInternalHttp.Port),
			};
			await _client.LeaderIsResigningAsync(request, deadline: deadline.ToUniversalTime());
		}

		private async Task SendLeaderIsResigningOkAsync(Guid leaderId, IPEndPoint leaderInternalHttp,
			Guid serverId, IPEndPoint serverInternalHttp, DateTime deadline) {
			var request = new LeaderIsResigningOkRequest {
				LeaderId = Uuid.FromGuid(leaderId).ToDto(),
				LeaderInternalHttp = new EndPoint(leaderInternalHttp.Address.ToString(), (uint)leaderInternalHttp.Port),
				ServerId = Uuid.FromGuid(serverId).ToDto(),
				ServerInternalHttp = new EndPoint(serverInternalHttp.Address.ToString(), (uint)serverInternalHttp.Port),
			};
			await _client.LeaderIsResigningOkAsync(request, deadline: deadline.ToUniversalTime());
		}
	}
}
