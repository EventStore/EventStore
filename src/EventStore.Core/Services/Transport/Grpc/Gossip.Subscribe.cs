#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Client.Gossip;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using Grpc.Core;
using Serilog;
using ClusterInfo = EventStore.Client.Gossip.ClusterInfo;
using Empty = EventStore.Client.Empty;
using MemberInfo = EventStore.Client.Gossip.MemberInfo;
using ServerClusterInfo = EventStore.Core.Cluster.ClusterInfo;

namespace EventStore.Core.Services.Transport.Grpc;

partial class Gossip {
	private static readonly ILogger Logger = Log.ForContext<Gossip>();

	public override async Task Subscribe(Empty request, IServerStreamWriter<ClusterInfo> responseStream,
		ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _authorizationProvider.CheckAccessAsync(user, ReadOperation, context.CancellationToken)) {
			throw RpcExceptions.AccessDenied();
		}

		await _gossipTracker.Track(responseStream, context.CancellationToken);
	}

	private class GossipTracker : IHandle<GossipMessage.GossipUpdated> {
		private readonly Channel<ServerClusterInfo> _channel;
		private readonly TaskCompletionSource _initialGossipReadSource;

		private ImmutableArray<IServerStreamWriter<ClusterInfo>> _streams;
		private ServerClusterInfo? _currentClusterInfo;
		private int _initialGossipRead;

		public GossipTracker(ISubscriber bus) {
			_streams = ImmutableArray<IServerStreamWriter<ClusterInfo>>.Empty;
			_channel = Channel.CreateBounded<ServerClusterInfo>(new BoundedChannelOptions(1) {
				SingleReader = true,
				SingleWriter = false,
				FullMode = BoundedChannelFullMode.DropOldest,
				AllowSynchronousContinuations = false
			});
			_initialGossipReadSource = new TaskCompletionSource();
			_initialGossipRead = 0;

			bus.Subscribe(this);

			PumpMessages();

			return;

			async void PumpMessages() {
				await foreach (var clusterInfo in _channel.Reader.ReadAllAsync().Select(ConvertClusterInfo)) {
					foreach (var stream in _streams) {
						try {
							await stream.WriteAsync(clusterInfo);
						} catch (Exception ex) {
							Logger.Warning(ex, "Could not send client gossip message.");
						}
					}
				}
			}
		}

		public async Task Track(IServerStreamWriter<ClusterInfo> responseStream, CancellationToken ct) {
			await _initialGossipReadSource.Task;

			await responseStream.WriteAsync(ConvertClusterInfo(_currentClusterInfo!), ct);

			var tcs = new TaskCompletionSource();

			await using var ctr = ct.Register(() => tcs.TrySetResult());

			ImmutableInterlocked.Update(ref _streams, streams => streams.Add(responseStream));

			await tcs.Task;

			ImmutableInterlocked.Update(ref _streams, streams => streams.Remove(responseStream));
		}


		public void Handle(GossipMessage.GossipUpdated message) {
			if (message.ClusterInfo.HasChangedSince(_currentClusterInfo)) {
				_currentClusterInfo = message.ClusterInfo;
				_channel.Writer.TryWrite(_currentClusterInfo);
			}

			if (Interlocked.CompareExchange(ref _initialGossipRead, 1, 0) == 0) {
				_initialGossipReadSource.SetResult();
			}
		}

		private static ClusterInfo ConvertClusterInfo(ServerClusterInfo clusterInfo) =>
			new() { Members = { Array.ConvertAll(clusterInfo.Members, ConvertMemberInfo) } };

		private static MemberInfo ConvertMemberInfo(Core.Cluster.MemberInfo memberInfo) => new() {
			InstanceId = Uuid.FromGuid(memberInfo.InstanceId).ToDto(),
			TimeStamp = memberInfo.TimeStamp.ToTicksSinceEpoch(),
			State = (MemberInfo.Types.VNodeState)memberInfo.State,
			IsAlive = memberInfo.IsAlive,
			HttpEndPoint = new EndPoint {
				Address = memberInfo.HttpEndPoint.GetHost(),
				Port = (uint)memberInfo.HttpEndPoint.GetPort()
			}
		};
	}
}
