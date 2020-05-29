using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client.Gossip;
using EventStore.Client.Shared;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using Grpc.Core;
using static EventStore.Common.Utils.EndpointExtensions;
using ClusterInfo = EventStore.Client.Gossip.ClusterInfo;
using MemberInfo = EventStore.Client.Gossip.MemberInfo;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Gossip {
		private static readonly Operation ReadOperation = new Operation(Plugins.Authorization.Operations.Node.Gossip.Read);
		public override async Task<ClusterInfo> Read(Empty request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, ReadOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			var tcs = new TaskCompletionSource<ClusterInfo>();
			_bus.Publish(new GossipMessage.GossipReceived(new CallbackEnvelope(msg => GossipResponse(msg, tcs)),
				new EventStore.Core.Cluster.ClusterInfo(), null));
			return await tcs.Task.ConfigureAwait(false);
		}

		private void GossipResponse(Message msg, TaskCompletionSource<ClusterInfo> tcs) {
			if (msg is GossipMessage.SendGossip received) {
				tcs.TrySetResult(ToGrpcClusterInfo(received.ClusterInfo));
			}
		}

		private ClusterInfo ToGrpcClusterInfo(Core.Cluster.ClusterInfo cluster) {
			var members = Array.ConvertAll(cluster.Members, x => new MemberInfo {
				InstanceId = Uuid.FromGuid(x.InstanceId).ToDto(),
				TimeStamp = x.TimeStamp.ToTicksSinceEpoch(),
				State = (MemberInfo.Types.VNodeState)x.State,
				IsAlive = x.IsAlive,
				HttpEndPoint = new EndPoint{
					Address = x.HttpEndPoint.GetHost(),
					Port = (uint)x.HttpEndPoint.GetPort()
				}
			}).ToArray();
			var info = new ClusterInfo();
			info.Members.AddRange(members);
			return info;
		}
	}
}
