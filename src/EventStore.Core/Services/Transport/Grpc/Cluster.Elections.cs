﻿using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.Shared;
using EventStore.Cluster;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Plugins.Authorization;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Elections {
		private static readonly Empty EmptyResult = new Empty();
		private readonly IPublisher _bus;
		private readonly IAuthorizationProvider _authorizationProvider;
		private static readonly Operation ViewChangeOperation = new Operation(Plugins.Authorization.Operations.Node.Elections.ViewChange);
		private static readonly Operation ViewChangeProofOperation = new Operation(Plugins.Authorization.Operations.Node.Elections.ViewChangeProof);
		private static readonly Operation PrepareOperation = new Operation(Plugins.Authorization.Operations.Node.Elections.Prepare);
		private static readonly Operation PrepareOkOperation = new Operation(Plugins.Authorization.Operations.Node.Elections.PrepareOk);
		private static readonly Operation ProposalOperation = new Operation(Plugins.Authorization.Operations.Node.Elections.Proposal);
		private static readonly Operation AcceptOperation = new Operation(Plugins.Authorization.Operations.Node.Elections.Accept);
		private static readonly Operation MasterIsResigningOperation = new Operation(Plugins.Authorization.Operations.Node.Elections.LeaderIsResigning);
		private static readonly Operation MasterIsResigningOkOperation = new Operation(Plugins.Authorization.Operations.Node.Elections.LeaderIsResigningOk);


		public Elections(IPublisher bus, IAuthorizationProvider authorizationProvider) {
			_bus = bus;
			_authorizationProvider = authorizationProvider ?? throw new ArgumentNullException(nameof(authorizationProvider));
		}
		
		public override async Task<Empty> ViewChange(ViewChangeRequest request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, ViewChangeOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			_bus.Publish(new ElectionMessage.ViewChange(
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address), (int)request.ServerInternalHttp.Port),
				request.AttemptedView));
			return EmptyResult;
		}
		
		public override async Task<Empty> ViewChangeProof(ViewChangeProofRequest request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, ViewChangeProofOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			_bus.Publish(new ElectionMessage.ViewChangeProof(
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address), (int)request.ServerInternalHttp.Port),
				request.InstalledView));
			return EmptyResult;
		}
		
		public override async Task<Empty> Prepare(PrepareRequest request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, PrepareOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			_bus.Publish(new ElectionMessage.Prepare(
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address), (int)request.ServerInternalHttp.Port),
				request.View));
			return EmptyResult;
		}
	
		public override async Task<Empty> PrepareOk(PrepareOkRequest request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, PrepareOkOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
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
			return EmptyResult;
		}
				
		public override async Task<Empty> Proposal(ProposalRequest request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, ProposalOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
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
			return EmptyResult;
		}

		public override async Task<Empty> Accept(AcceptRequest request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, AcceptOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			_bus.Publish(new ElectionMessage.Accept(
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address),
					(int)request.ServerInternalHttp.Port),
				Uuid.FromDto(request.LeaderId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.LeaderInternalHttp.Address),
					(int)request.LeaderInternalHttp.Port),
				request.View));
			return EmptyResult;
		}

		public override async Task<Empty> LeaderIsResigning(LeaderIsResigningRequest request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, MasterIsResigningOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			_bus.Publish(new ElectionMessage.LeaderIsResigning(
				Uuid.FromDto(request.LeaderId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.LeaderInternalHttp.Address),
					(int)request.LeaderInternalHttp.Port)));
			return EmptyResult;
		}
		
		public override async Task<Empty> LeaderIsResigningOk(LeaderIsResigningOkRequest request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, MasterIsResigningOkOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			_bus.Publish(new ElectionMessage.LeaderIsResigningOk(
				Uuid.FromDto(request.LeaderId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.LeaderInternalHttp.Address),
					(int)request.LeaderInternalHttp.Port),
				Uuid.FromDto(request.ServerId).ToGuid(),
				new IPEndPoint(IPAddress.Parse(request.ServerInternalHttp.Address),
					(int)request.ServerInternalHttp.Port)));
			return EmptyResult;
		}
	}
}
