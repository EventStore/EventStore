extern alias GrpcClient;
extern alias GrpcClientStreams;
using System.Threading.Tasks;
using EventStore.Core.Tests.ClientAPI.Helpers;
using GrpcClientStreams::EventStore.Client;
using NUnit.Framework;
using EventData = GrpcClient::EventStore.Client.EventData;
using Uuid = GrpcClient::EventStore.Client.Uuid;
using WrongExpectedVersionException = EventStore.Core.Tests.ClientAPI.Helpers.WrongExpectedVersionException;

namespace EventStore.Core.Tests.ClientAPI.ExpectedVersion64Bit {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	[Category("ClientAPI"), Category("LongRunning")]
	public class append_to_stream_with_event_numbers_greater_than_2_billion<TLogFormat, TStreamId>
		: MiniNodeWithExistingRecords<TLogFormat, TStreamId> {
		private const string StreamName = "append_to_stream_with_event_numbers_greater_than_2_billion";
		private const long intMaxValue = (long)int.MaxValue;

		public override void WriteTestScenario() {
			WriteSingleEvent(StreamName, intMaxValue + 1, new string('.', 3000));
			WriteSingleEvent(StreamName, intMaxValue + 2, new string('.', 3000));
			WriteSingleEvent(StreamName, intMaxValue + 3, new string('.', 3000));
			WriteSingleEvent(StreamName, intMaxValue + 4, new string('.', 3000));
			WriteSingleEvent(StreamName, intMaxValue + 5, new string('.', 3000));
		}

		public override async Task Given() {
			_store = BuildConnection(Node);
			await _store.ConnectAsync();
			await _store.SetStreamMetadataAsync(StreamName, EventStore.ClientAPI.ExpectedVersion.Any,
				new StreamMetadata(truncateBefore: intMaxValue + 1));
		}

		[Test]
		public async Task should_be_able_to_append_to_stream() {
			var evnt = new EventData(Uuid.NewUuid(), "EventType", new byte[10], new byte[15]);
			var writeResult = await _store.AppendToStreamAsync(StreamName, intMaxValue + 5, evnt);
			Assert.AreEqual(intMaxValue + 6, writeResult.NextExpectedVersion);

			var readResult = await _store
				.ReadStreamEventsForwardAsync(StreamName, intMaxValue + 6, 1, false, DefaultData.AdminCredentials);
			Assert.AreEqual(SliceReadStatus.Success, readResult.Status);
			Assert.AreEqual(evnt.EventId, readResult.Events[0].Event.EventId);
		}

		[Test]
		public async Task should_throw_wrong_expected_version_when_version_incorrect() {
			var evnt = new EventData(Uuid.NewUuid(), "EventType", new byte[10], new byte[15]);
			await AssertEx.ThrowsAsync<WrongExpectedVersionException>(
				() => _store.AppendToStreamAsync(StreamName, intMaxValue + 15, evnt));
		}
	}
}
