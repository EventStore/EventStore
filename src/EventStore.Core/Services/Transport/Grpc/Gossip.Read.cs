using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client.Gossip;
using EventStore.Client;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using Grpc.Core;
using ClusterInfo = EventStore.Client.Gossip.ClusterInfo;
using MemberInfo = EventStore.Client.Gossip.MemberInfo;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Gossip {
		private static readonly Operation ReadOperation = new Operation(Plugins.Authorization.Operations.Node.Gossip.ClientRead);
		public override async Task<ClusterInfo> Read(Empty request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, ReadOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			var tcs = new TaskCompletionSource<ClusterInfo>();
			_bus.Publish(new GossipMessage.ClientGossip(new CallbackEnvelope(msg => GossipResponse(msg, tcs))));;
			return await tcs.Task.ConfigureAwait(false);
		}

		private void GossipResponse(Message msg, TaskCompletionSource<ClusterInfo> tcs) {
			if (msg is GossipMessage.SendClientGossip received) {
				tcs.TrySetResult(ToGrpcClusterInfo(received.ClusterInfo));
			}
		}

		private ClusterInfo ToGrpcClusterInfo(Core.Cluster.ClientClusterInfo cluster) {
			var members = Array.ConvertAll(cluster.Members, x => new MemberInfo {
				InstanceId = Uuid.FromGuid(x.InstanceId).ToDto(),
				TimeStamp = x.TimeStamp.ToTicksSinceEpoch(),
				State = (MemberInfo.Types.VNodeState)x.State,
				IsAlive = x.IsAlive,
				HttpEndPoint = new EndPoint{
					Address = x.HttpEndPointIp,
					Port = (uint)x.HttpEndPointPort
				}
			}).ToArray();
			var info = new ClusterInfo();
			info.Members.AddRange(members);
			return info;
		}
	}
}
