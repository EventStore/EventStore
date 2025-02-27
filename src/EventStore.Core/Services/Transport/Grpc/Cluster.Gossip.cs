// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.Cluster;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Metrics;
using EventStore.Plugins.Authorization;
using Grpc.Core;
using Empty = EventStore.Client.Empty;

namespace EventStore.Core.Services.Transport.Grpc.Cluster;

partial class Gossip {
	private readonly IPublisher _bus;
	private readonly IAuthorizationProvider _authorizationProvider;
	private readonly string _clusterDns;
	private readonly IDurationTracker _updateTracker;
	private readonly IDurationTracker _readTracker;
	private static readonly Operation ReadOperation = new Operation(Plugins.Authorization.Operations.Node.Gossip.Read);
	private static readonly Operation UpdateOperation = new Operation(Plugins.Authorization.Operations.Node.Gossip.Update);

	public Gossip(IPublisher bus, IAuthorizationProvider authorizationProvider, string clusterDns,
		IDurationTracker updateTracker,
		IDurationTracker readTracker) {

		_bus = bus;
		_authorizationProvider = authorizationProvider ?? throw new ArgumentNullException(nameof(authorizationProvider));
		_clusterDns = clusterDns;
		_updateTracker = updateTracker;
		_readTracker = readTracker;
	}

	public override async Task<ClusterInfo> Update(GossipRequest request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, UpdateOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}
		var clusterInfo = EventStore.Core.Cluster.ClusterInfo.FromGrpcClusterInfo(request.Info, _clusterDns);
		var tcs = new TaskCompletionSource<ClusterInfo>();
		var duration = _updateTracker.Start();
		_bus.Publish(new GossipMessage.GossipReceived(new CallbackEnvelope(msg => GossipResponse(msg, tcs, duration)),
			clusterInfo, new DnsEndPoint(request.Server.Address, (int)request.Server.Port).WithClusterDns(_clusterDns)));
		return await tcs.Task;
	}

	public override async Task<ClusterInfo> Read(Empty request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, ReadOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}
		var tcs = new TaskCompletionSource<ClusterInfo>();
		var duration = _readTracker.Start();
		_bus.Publish(new GossipMessage.ReadGossip(new CallbackEnvelope(msg => GossipResponse(msg, tcs, duration))));
		return await tcs.Task;
	}

	private void GossipResponse(Message msg, TaskCompletionSource<ClusterInfo> tcs, Duration duration) {
		if (msg is GossipMessage.SendGossip received) {
			tcs.TrySetResult(EventStore.Core.Cluster.ClusterInfo.ToGrpcClusterInfo(received.ClusterInfo));
			duration.Dispose();
		}
	}
}
