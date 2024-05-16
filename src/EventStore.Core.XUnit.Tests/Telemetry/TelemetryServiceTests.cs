using System;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Telemetry;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Plugins;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Telemetry;

public sealed class TelemetryServiceTests : IAsyncLifetime {
	private readonly TelemetryService _sut;
	private readonly InMemoryTelemetrySink _sink;
	private readonly ChannelReader<Message> _channelReader;
	private readonly TFChunkDb _db;
	private readonly DirectoryFixture<TelemetryServiceTests> _fixture = new();

	public TelemetryServiceTests() {
		var config = TFChunkHelper.CreateSizedDbConfig(_fixture.Directory, 0, chunkSize: 4096);
		_db = new TFChunkDb(config);
		_db.Open(false);
		var channel = Channel.CreateUnbounded<Message>();
		_channelReader = channel.Reader;
		_sink = new InMemoryTelemetrySink();
		_sut =  new TelemetryService(
			_db.Manager,
			new ClusterVNodeOptions().WithPlugableComponent(new FakePlugableComponent()),
			new EnvelopePublisher(new ChannelEnvelope(channel)),
			_sink,
			new InMemoryCheckpoint(0),
			Guid.NewGuid());
	}

	public Task InitializeAsync() => _fixture.InitializeAsync();

	public async Task DisposeAsync() {
		_sut.Dispose();
		_db.Close();
		await _fixture.DisposeAsync();
	}

	private static MemberInfo CreateMemberInfo(Guid instanceId) {
		static int random() => Random.Shared.Next(65000);

		var memberInfo = MemberInfo.ForVNode(
				instanceId: instanceId,
				timeStamp: DateTime.Now,
				state: Data.VNodeState.DiscoverLeader,
				isAlive: true,
				internalTcpEndPoint: default,
				internalSecureTcpEndPoint: new DnsEndPoint("myhost", random()),
				externalTcpEndPoint: default,
				externalSecureTcpEndPoint: new DnsEndPoint("myhost", random()),
				httpEndPoint: new DnsEndPoint("myhost", random()),
				advertiseHostToClientAs: "advertiseHostToClientAs",
				advertiseHttpPortToClientAs: random(),
				advertiseTcpPortToClientAs: random(),
				lastCommitPosition: random(),
				writerCheckpoint: random(),
				chaserCheckpoint: random(),
				epochPosition: random(),
				epochNumber: random(),
				epochId: Guid.NewGuid(),
				nodePriority: random(),
				isReadOnlyReplica: true);

		return memberInfo;
	}

	[Fact]
	public async Task can_collect_and_flush_telemetry() {
		// receive schedule of collect trigger it
		var schedule = Assert.IsType<TimerMessage.Schedule>(await _channelReader.ReadAsync());
		Assert.IsType<TelemetryMessage.Collect>(schedule.ReplyMessage);
		schedule.Reply();

		// receive the gossip request the telemetry service sends.
		var gossipRequest = Assert.IsType<GossipMessage.ReadGossip>(await _channelReader.ReadAsync());
		gossipRequest.Envelope.ReplyWith(new GossipMessage.SendGossip(
			new ClusterInfo(
				CreateMemberInfo(Guid.Empty),
				CreateMemberInfo(Guid.Empty)),
			new DnsEndPoint("localhost", 123)));

		// receive usage request and send response
		var request = Assert.IsType<TelemetryMessage.Request>(await _channelReader.ReadAsync());
		request.Envelope.ReplyWith(new TelemetryMessage.Response(
			"foo",
			new JsonObject {
				["bar"] = 42,
			}));

		// receive schedule of flush and trigger it
		schedule = Assert.IsType<TimerMessage.Schedule>(await _channelReader.ReadAsync());
		Assert.IsType<TelemetryMessage.Flush>(schedule.ReplyMessage);
		schedule.Reply();

		// receive schedule of collect indicating flush is complete
		schedule = Assert.IsType<TimerMessage.Schedule>(await _channelReader.ReadAsync());
		Assert.IsType<TelemetryMessage.Collect>(schedule.ReplyMessage);

		// check sink has received the data
		Assert.NotNull(_sink.Data);
		Assert.NotNull(_sink.Data["foo"]);
		Assert.Equal(new JsonObject { ["bar"] = 42 }.ToString(), _sink.Data["foo"].ToString());

		Assert.NotNull(_sink.Data["plugins"]);
		Assert.Equal("""
			{
			  "fakeComponent": {
			    "foo": "bar"
			  }
			}
			""",
			_sink.Data["plugins"].ToString());
	}

	private class FakePlugableComponent : IPlugableComponent {
		public void CollectTelemetry(Action<string, JsonNode> reply) {
			reply("fakeComponent", new JsonObject {
				["foo"] = "bar"
			});
		}
	}
}
