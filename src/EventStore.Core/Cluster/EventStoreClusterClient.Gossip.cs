using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.Shared;
using EventStore.Cluster;
using EventStore.Common.Log;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using ILogger = Serilog.ILogger;
using EndPoint = EventStore.Cluster.EndPoint;

namespace EventStore.Core.Cluster {
	public partial class EventStoreClusterClient {
		private static readonly ILogger Log = Serilog.Log.ForContext<EventStoreClusterClient>();

		public void SendGossip(GossipMessage.SendGossip sendGossip, IPEndPoint destinationEndpoint, DateTime deadline) {
			SendGossipAsync(sendGossip.ClusterInfo, sendGossip.ServerEndPoint, deadline).ContinueWith(
				async response => {
					try {
						_bus.Publish(new GossipMessage.GossipReceived(new CallbackEnvelope(_ => { }),
							await response.ConfigureAwait(false), destinationEndpoint));
					} catch (Exception ex) {
						_bus.Publish(new GossipMessage.GossipSendFailed(ex.Message, destinationEndpoint));
					}
				});
		}

		public void GetGossip(IPEndPoint destinationEndpoint, DateTime deadline) {
			GetGossipAsync(deadline).ContinueWith(async response => {
				try {
					_bus.Publish(new GossipMessage.GetGossipReceived(await response.ConfigureAwait(false),
						destinationEndpoint));
				} catch (Exception ex) {
					_bus.Publish(new GossipMessage.GetGossipFailed(ex.Message, destinationEndpoint));
				}
			});
		}

		private async Task<ClusterInfo> SendGossipAsync(ClusterInfo clusterInfo,
			IPEndPoint server, DateTime deadline) {
			var request = new GossipRequest {
				Info = ClusterInfo.ToGrpcClusterInfo(clusterInfo),
				Server = new EndPoint(server.Address.ToString(), (uint)server.Port)
			};
			var clusterInfoDto = await _client.GossipAsync(request, deadline: deadline.ToUniversalTime());
			return ClusterInfo.FromGrpcClusterInfo(clusterInfoDto);
		}

		private async Task<ClusterInfo> GetGossipAsync(DateTime deadline) {
			var clusterInfoDto = await _client.GetGossipAsync(new Empty(), deadline: deadline.ToUniversalTime());
			return ClusterInfo.FromGrpcClusterInfo(clusterInfoDto);
		}
	}
}
