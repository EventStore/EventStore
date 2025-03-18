// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System.Net;
using System.Threading.Tasks;
using EventStore.Cluster;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Plugins.Authorization;
using Grpc.Core;
using static EventStore.Core.Messages.ElectionMessage;
using ClusterInfo = EventStore.Core.Cluster.ClusterInfo;
using Empty = EventStore.Client.Empty;

// ReSharper disable once CheckNamespace
namespace EventStore.Core.Services.Transport.Grpc.Cluster;

partial class Elections(IPublisher bus, IAuthorizationProvider authorizationProvider, string clusterDns) {
	private static readonly Empty EmptyResult = new();
	private readonly IAuthorizationProvider _authorizationProvider = Ensure.NotNull(authorizationProvider);
	private static readonly Operation ViewChangeOperation = new(Plugins.Authorization.Operations.Node.Elections.ViewChange);
	private static readonly Operation ViewChangeProofOperation = new(Plugins.Authorization.Operations.Node.Elections.ViewChangeProof);
	private static readonly Operation PrepareOperation = new(Plugins.Authorization.Operations.Node.Elections.Prepare);
	private static readonly Operation PrepareOkOperation = new(Plugins.Authorization.Operations.Node.Elections.PrepareOk);
	private static readonly Operation ProposalOperation = new(Plugins.Authorization.Operations.Node.Elections.Proposal);
	private static readonly Operation AcceptOperation = new(Plugins.Authorization.Operations.Node.Elections.Accept);
	private static readonly Operation MasterIsResigningOperation = new(Plugins.Authorization.Operations.Node.Elections.LeaderIsResigning);
	private static readonly Operation MasterIsResigningOkOperation = new(Plugins.Authorization.Operations.Node.Elections.LeaderIsResigningOk);


	public override async Task<Empty> ViewChange(ViewChangeRequest request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, ViewChangeOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}

		bus.Publish(new ViewChange(
			Uuid.FromDto(request.ServerId).ToGuid(),
			new DnsEndPoint(request.ServerHttp.Address, (int)request.ServerHttp.Port).WithClusterDns(clusterDns),
			request.AttemptedView)
		);
		return EmptyResult;
	}

	public override async Task<Empty> ViewChangeProof(ViewChangeProofRequest request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, ViewChangeProofOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}

		bus.Publish(new ViewChangeProof(
			Uuid.FromDto(request.ServerId).ToGuid(),
			new DnsEndPoint(request.ServerHttp.Address, (int)request.ServerHttp.Port).WithClusterDns(clusterDns),
			request.InstalledView)
		);
		return EmptyResult;
	}

	public override async Task<Empty> Prepare(PrepareRequest request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, PrepareOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}

		bus.Publish(new Prepare(
			Uuid.FromDto(request.ServerId).ToGuid(),
			new DnsEndPoint(request.ServerHttp.Address, (int)request.ServerHttp.Port).WithClusterDns(clusterDns),
			request.View)
		);
		return EmptyResult;
	}

	public override async Task<Empty> PrepareOk(PrepareOkRequest request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, PrepareOkOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}

		bus.Publish(new PrepareOk(
			request.View,
			Uuid.FromDto(request.ServerId).ToGuid(),
			new DnsEndPoint(request.ServerHttp.Address, (int)request.ServerHttp.Port).WithClusterDns(clusterDns),
			request.EpochNumber,
			request.EpochPosition,
			Uuid.FromDto(request.EpochId).ToGuid(),
			Uuid.FromDto(request.EpochLeaderInstanceId).ToGuid(),
			request.LastCommitPosition,
			request.WriterCheckpoint,
			request.ChaserCheckpoint,
			request.NodePriority,
			ClusterInfo.FromGrpcClusterInfo(request.ClusterInfo, clusterDns))
		);
		return EmptyResult;
	}

	public override async Task<Empty> Proposal(ProposalRequest request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, ProposalOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}

		bus.Publish(new Proposal(
			Uuid.FromDto(request.ServerId).ToGuid(),
			new DnsEndPoint(request.ServerHttp.Address, (int)request.ServerHttp.Port).WithClusterDns(clusterDns),
			Uuid.FromDto(request.LeaderId).ToGuid(),
			new DnsEndPoint(request.LeaderHttp.Address, (int)request.LeaderHttp.Port).WithClusterDns(clusterDns),
			request.View,
			request.EpochNumber,
			request.EpochPosition,
			Uuid.FromDto(request.EpochId).ToGuid(),
			Uuid.FromDto(request.EpochLeaderInstanceId).ToGuid(),
			request.LastCommitPosition,
			request.WriterCheckpoint,
			request.ChaserCheckpoint,
			request.NodePriority)
		);
		return EmptyResult;
	}

	public override async Task<Empty> Accept(AcceptRequest request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, AcceptOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}

		bus.Publish(new Accept(
			Uuid.FromDto(request.ServerId).ToGuid(),
			new DnsEndPoint(request.ServerHttp.Address, (int)request.ServerHttp.Port).WithClusterDns(clusterDns),
			Uuid.FromDto(request.LeaderId).ToGuid(),
			new DnsEndPoint(request.LeaderHttp.Address, (int)request.LeaderHttp.Port).WithClusterDns(clusterDns),
			request.View)
		);
		return EmptyResult;
	}

	public override async Task<Empty> LeaderIsResigning(LeaderIsResigningRequest request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, MasterIsResigningOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}

		bus.Publish(new LeaderIsResigning(
			Uuid.FromDto(request.LeaderId).ToGuid(),
			new DnsEndPoint(request.LeaderHttp.Address, (int)request.LeaderHttp.Port).WithClusterDns(clusterDns))
		);
		return EmptyResult;
	}

	public override async Task<Empty> LeaderIsResigningOk(LeaderIsResigningOkRequest request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, MasterIsResigningOkOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}

		bus.Publish(new LeaderIsResigningOk(
			Uuid.FromDto(request.LeaderId).ToGuid(),
			new DnsEndPoint(request.LeaderHttp.Address, (int)request.LeaderHttp.Port).WithClusterDns(clusterDns),
			Uuid.FromDto(request.ServerId).ToGuid(),
			new DnsEndPoint(request.ServerHttp.Address, (int)request.ServerHttp.Port).WithClusterDns(clusterDns))
		);
		return EmptyResult;
	}
}
