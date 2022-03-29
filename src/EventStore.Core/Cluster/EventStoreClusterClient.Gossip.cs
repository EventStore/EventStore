using System;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Common.Utils;
using Empty = EventStore.Client.Empty;
using ILogger = Serilog.ILogger;
using EndPoint = System.Net.EndPoint;
using GossipEndPoint = EventStore.Cluster.EndPoint;

namespace EventStore.Core.Cluster {
	public partial class EventStoreClusterClient {
		private static readonly ILogger Log = Serilog.Log.ForContext<EventStoreClusterClient>();

		public void SendGossip(GossipMessage.SendGossip sendGossip, EndPoint destinationEndpoint, DateTime deadline) {
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

		public void GetGossip(EndPoint destinationEndpoint, DateTime deadline) {
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
			EndPoint server, DateTime deadline) {
			var request = new GossipRequest {
				Info = ClusterInfo.ToGrpcClusterInfo(clusterInfo),
				Server = new GossipEndPoint(server.GetHost(), (uint)server.GetPort())
			};
			var clusterInfoDto = await _gossipClient.UpdateAsync(request, deadline: deadline.ToUniversalTime());
			return ClusterInfo.FromGrpcClusterInfo(clusterInfoDto, _clusterDns);
		}

		private async Task<ClusterInfo> GetGossipAsync(DateTime deadline) {
			var clusterInfoDto = await _gossipClient.ReadAsync(new Empty(), deadline: deadline.ToUniversalTime());
			return ClusterInfo.FromGrpcClusterInfo(clusterInfoDto, _clusterDns);
		}
	}
}
