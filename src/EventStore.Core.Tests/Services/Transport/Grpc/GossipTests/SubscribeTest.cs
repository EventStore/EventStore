using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Gossip;
using EventStore.Core.Data;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Tests.Integration;
using Grpc.Net.Client;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Grpc.GossipTests;

public class SubscribeTest {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_subscribing_to_a_cluster<TLogFormat, TStreamId>
		: specification_with_cluster<TLogFormat, TStreamId> {
		private readonly List<MemberInfo> _memberInfo = new();

		protected override async Task Given() {
			var leader = GetLeader();

			await leader.AdminUserCreated;

			SpinWait.SpinUntil(() => _nodes.All(node => node.NodeState is VNodeState.Follower or VNodeState.Leader));

			using var channel = GrpcChannel.ForAddress(new Uri($"https://{leader.HttpEndPoint}"),
				new GrpcChannelOptions {
					HttpClient = new HttpClient(new SocketsHttpHandler {
						SslOptions = {
							RemoteCertificateValidationCallback = delegate { return true; }
						}
					}, true)
				});
			var client = new Gossip.GossipClient(channel);
			using var call = client.Subscribe(new());

			while (await call.ResponseStream.MoveNext(CancellationToken.None)) {
				if (call.ResponseStream.Current.Members.Any(member =>
					    member.State is not (MemberInfo.Types.VNodeState.Leader
						    or MemberInfo.Types.VNodeState.Follower))) {
					continue;
				}

				_memberInfo.AddRange(call.ResponseStream.Current.Members);

				break;
			}
		}

		[Test]
		public void should_contain_the_cluster_info() {
			var expected = _nodes.Select(node => new {
				node.NodeState,
				node.HttpEndPoint,
				IsAlive = true,
				node.Node.NodeInfo.InstanceId
			});
			var actual = _memberInfo.Select(info => new {
				NodeState = (VNodeState)info.State,
				HttpEndPoint = new IPEndPoint(IPAddress.Parse(info.HttpEndPoint.Address), (int)info.HttpEndPoint.Port),
				info.IsAlive,
				InstanceId = Uuid.FromDto(info.InstanceId).ToGuid()
			});

			CollectionAssert.AreEquivalent(expected, actual);
		}
	}
}
