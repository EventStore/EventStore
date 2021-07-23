using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Cluster;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc.Cluster {
	partial class Gossip {
		private readonly IPublisher _bus;
		private readonly IAuthorizationProvider _authorizationProvider;
		private static readonly Operation ReadOperation = new Operation(Plugins.Authorization.Operations.Node.Gossip.Read);
		private static readonly Operation UpdateOperation = new Operation(Plugins.Authorization.Operations.Node.Gossip.Update);
		public Gossip(IPublisher bus, IAuthorizationProvider authorizationProvider) {
			_bus = bus;
			_authorizationProvider = authorizationProvider ?? throw new ArgumentNullException(nameof(authorizationProvider));
		}

		public override async Task<ClusterInfo> Update(GossipRequest request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, UpdateOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			var clusterInfo = EventStore.Core.Cluster.ClusterInfo.FromGrpcClusterInfo(request.Info);
			var tcs = new TaskCompletionSource<ClusterInfo>();
			_bus.Publish(new GossipMessage.GossipReceived(new CallbackEnvelope(msg => GossipResponse(msg, tcs)),
				clusterInfo, new DnsEndPoint(request.Server.Address, (int)request.Server.Port)));
			return await tcs.Task.ConfigureAwait(false);
		}

		public override async Task<ClusterInfo> Read(Empty request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, ReadOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			var tcs = new TaskCompletionSource<ClusterInfo>();
			_bus.Publish(new GossipMessage.ReadGossip(new CallbackEnvelope(msg => GossipResponse(msg, tcs))));
			return await tcs.Task.ConfigureAwait(false);
		}

		private void GossipResponse(Message msg, TaskCompletionSource<ClusterInfo> tcs) {
			if (msg is GossipMessage.SendGossip received) {
				tcs.TrySetResult(EventStore.Core.Cluster.ClusterInfo.ToGrpcClusterInfo(received.ClusterInfo));
			}
		}
	}
}
