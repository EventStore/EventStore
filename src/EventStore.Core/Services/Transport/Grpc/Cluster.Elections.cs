using System.Net;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.Shared;
using EventStore.Cluster;
using EventStore.Core.Messages;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Cluster {
		public override Task<Empty> ViewChange(ViewChangeRequest request, ServerCallContext context) {
			_bus.Publish(new ElectionMessage.ViewChange(
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address), (int)request.ServerInternalHttp.Port),
				request.AttemptedView));
			return Task.FromResult(new Empty());
		}
		
		public override Task<Empty> ViewChangeProof(ViewChangeProofRequest request, ServerCallContext context) {
			_bus.Publish(new ElectionMessage.ViewChangeProof(
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address), (int)request.ServerInternalHttp.Port),
				request.InstalledView));
			return Task.FromResult(new Empty());
		}
		
		public override Task<Empty> Prepare(PrepareRequest request, ServerCallContext context) {
			_bus.Publish(new ElectionMessage.Prepare(
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address), (int)request.ServerInternalHttp.Port),
				request.View));
			return Task.FromResult(new Empty());
		}
	
		public override Task<Empty> PrepareOk(PrepareOkRequest request, ServerCallContext context) {
			_bus.Publish(new ElectionMessage.PrepareOk(
				request.View,
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address), (int)request.ServerInternalHttp.Port),
				request.EpochNumber,
				request.EpochPosition,
				Uuid.FromDto(request.EpochId).ToGuid(),
				request.LastCommitPosition,
				request.WriterCheckpoint,
				request.ChaserCheckpoint,
				request.NodePriority));
			return Task.FromResult(new Empty());
		}
				
		public override Task<Empty> Proposal(ProposalRequest request, ServerCallContext context) {
			_bus.Publish(new ElectionMessage.Proposal(
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address), (int)request.ServerInternalHttp.Port),
				Uuid.FromDto(request.LeaderId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.LeaderInternalHttp.Address), (int)request.LeaderInternalHttp.Port),
				request.View,
				request.EpochNumber,
				request.EpochPosition,
				Uuid.FromDto(request.EpochId).ToGuid(),
				request.LastCommitPosition,
				request.WriterCheckpoint,
				request.ChaserCheckpoint,
				request.NodePriority));
			return Task.FromResult(new Empty());
		}

		public override Task<Empty> Accept(AcceptRequest request, ServerCallContext context) {
			_bus.Publish(new ElectionMessage.Accept(
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address),
					(int)request.ServerInternalHttp.Port),
				Uuid.FromDto(request.LeaderId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.LeaderInternalHttp.Address),
					(int)request.LeaderInternalHttp.Port),
				request.View));
			return Task.FromResult(new Empty());
		}

		public override Task<Empty> LeaderIsResigning(LeaderIsResigningRequest request, ServerCallContext context) {
			_bus.Publish(new ElectionMessage.LeaderIsResigning(
				Uuid.FromDto(request.LeaderId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.LeaderInternalHttp.Address),
					(int)request.LeaderInternalHttp.Port)));
			return Task.FromResult(new Empty());
		}
		
		public override Task<Empty> LeaderIsResigningOk(LeaderIsResigningOkRequest request, ServerCallContext context) {
			_bus.Publish(new ElectionMessage.LeaderIsResigningOk(
				Uuid.FromDto(request.LeaderId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.LeaderInternalHttp.Address),
					(int)request.LeaderInternalHttp.Port),
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address),
					(int)request.ServerInternalHttp.Port)));
			return Task.FromResult(new Empty());
		}
	}
}
