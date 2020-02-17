using System.Net;
using System.Threading.Tasks;
using EventStore.Client.Shared;
using EventStore.Cluster;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Cluster {
		private readonly IPublisher _bus;

		public Cluster(IPublisher bus) {
			_bus = bus;
		}

		public override async Task<ClusterInfo> Gossip(GossipRequest request, ServerCallContext context) {
			var clusterInfo = EventStore.Core.Cluster.ClusterInfo.FromGrpcClusterInfo(request.Info);
			var tcs = new TaskCompletionSource<ClusterInfo>();
			_bus.Publish(new GossipMessage.GossipReceived(new CallbackEnvelope(msg => GossipResponse(msg, tcs)),
				clusterInfo, new IPEndPoint(IPAddress.Parse(request.Server.Address), (int)request.Server.Port)));
			return await tcs.Task.ConfigureAwait(false);
		}

		public override async Task<ClusterInfo> GetGossip(Empty request, ServerCallContext context) {
			var tcs = new TaskCompletionSource<ClusterInfo>();
			_bus.Publish(new GossipMessage.GossipReceived(new CallbackEnvelope(msg => GossipResponse(msg, tcs)),
				new EventStore.Core.Cluster.ClusterInfo(), null));
			return await tcs.Task.ConfigureAwait(false);
		}

		private void GossipResponse(Message msg, TaskCompletionSource<ClusterInfo> tcs) {
			if (msg is GossipMessage.SendGossip received) {
				tcs.TrySetResult(EventStore.Core.Cluster.ClusterInfo.ToGrpcClusterInfo(received.ClusterInfo));
			}
		}
	}
}
