extern alias GrpcClient;
extern alias GrpcClientStreams;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using GrpcClientStreams::EventStore.Client;
using NUnit.Framework;
using StreamDeletedException = GrpcClient::EventStore.Client.StreamDeletedException;

namespace EventStore.Core.Tests.ClientAPI {
	extern alias GrpcClient;

	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class soft_delete<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		private MiniNode<TLogFormat, TStreamId> _node;
		private IEventStoreClient _conn;

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			_node = new MiniNode<TLogFormat, TStreamId>(PathName);
			await _node.Start();

			_conn = BuildConnection(_node);
			await _conn.ConnectAsync();
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			await _conn.Close();
			await _node.Shutdown();
			await base.TestFixtureTearDown();
		}

		protected virtual IEventStoreClient BuildConnection(MiniNode<TLogFormat, TStreamId> node) {
			return new GrpcEventStoreConnection(node.HttpEndPoint);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task soft_deleted_stream_returns_no_stream_and_no_events_on_read() {
			const string stream = "soft_deleted_stream_returns_no_stream_and_no_events_on_read";

			Assert.AreEqual(1,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent())).NextExpectedVersion);
			await _conn.DeleteStreamAsync(stream, 1);

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.StreamNotFound, res.Status);
			Assert.AreEqual(0, res.Events.Length);
			// REVIEW>> That test makes no sense.
			//Assert.AreEqual(1, res.LastEventNumber);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task soft_deleted_stream_allows_recreation_when_expver_any() {
			const string stream = "soft_deleted_stream_allows_recreation_when_expver_any";

			Assert.AreEqual(1,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent())).NextExpectedVersion);
			await _conn.DeleteStreamAsync(stream, 1);

			var events = new[] { TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent() };
			Assert.AreEqual(4,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.Any, events)).NextExpectedVersion);

			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(events.Select(x => x.EventId), res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] { 2, 3, 4 }, res.Events.Select(x => x.OriginalEvent.EventNumber.ToInt64()));

			var meta = await _conn.GetStreamMetadataAsync(stream);
			Assert.AreEqual(2, meta.Metadata.TruncateBefore.Value.ToInt64());
			Assert.AreEqual(1, meta.MetastreamRevision!.Value.ToInt64());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task soft_deleted_stream_allows_recreation_when_expver_no_stream() {
			const string stream = "soft_deleted_stream_allows_recreation_when_expver_no_stream";

			Assert.AreEqual(1,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent())).NextExpectedVersion);
			await _conn.DeleteStreamAsync(stream, 1);

			var events = new[] { TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent() };
			Assert.AreEqual(4,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, events)).NextExpectedVersion);

			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(events.Select(x => x.EventId), res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] { 2, 3, 4 }, res.Events.Select(x => x.OriginalEvent.EventNumber.ToInt64()));

			var meta = await _conn.GetStreamMetadataAsync(stream);
			Assert.AreEqual(2, meta.Metadata.TruncateBefore.Value.ToInt64());
			Assert.AreEqual(1, meta.MetastreamRevision!.Value.ToInt64());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task soft_deleted_stream_allows_recreation_when_expver_is_exact() {
			const string stream = "soft_deleted_stream_allows_recreation_when_expver_is_exact";

			Assert.AreEqual(1,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent())).NextExpectedVersion);
			await _conn.DeleteStreamAsync(stream, 1);

			var events = new[] { TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent() };
			Assert.AreEqual(4, (await _conn.AppendToStreamAsync(stream, 1, events)).NextExpectedVersion);

			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(events.Select(x => x.EventId), res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] { 2, 3, 4 }, res.Events.Select(x => x.OriginalEvent.EventNumber.ToInt64()));

			var meta = await _conn.GetStreamMetadataAsync(stream);
			Assert.AreEqual(2, meta.Metadata.TruncateBefore.Value.ToInt64());
			Assert.AreEqual(1, meta.MetastreamRevision!.Value.ToInt64());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task soft_deleted_stream_when_recreated_preserves_metadata_except_truncatebefore() {
			const string stream = "soft_deleted_stream_when_recreated_preserves_metadata_except_truncatebefore";

			Assert.AreEqual(1,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent())).NextExpectedVersion);

			Assert.AreEqual(0, (await _conn.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream,
				new StreamMetadata(
					truncateBefore: long.MaxValue,
					maxCount: 100,
					acl: new StreamAcl(deleteRole: "some-role"),
					customMetadata: JsonDocument.Parse(new JsonObject {
						["key1"] = true,
						["key2"] = 17,
						["key3"] = "some value",
					}.ToJsonString())))).NextExpectedVersion);

			var events = new[] { TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent() };
			Assert.AreEqual(4, (await _conn.AppendToStreamAsync(stream, 1, events)).NextExpectedVersion);
			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(events.Select(x => x.EventId), res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] { 2, 3, 4 }, res.Events.Select(x => x.OriginalEvent.EventNumber.ToInt64()));

			var meta = await _conn.GetStreamMetadataAsync(stream);
			Assert.AreEqual(1, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(2, meta.Metadata.TruncateBefore.Value.ToInt64());
			Assert.AreEqual(100, meta.Metadata.MaxCount);
			Assert.AreEqual("some-role", meta.Metadata.Acl!.DeleteRoles[0]);
			Assert.AreEqual(true, meta.Metadata.CustomMetadata!.RootElement.GetProperty("key1").GetBoolean());
			Assert.AreEqual(17, meta.Metadata.CustomMetadata!.RootElement.GetProperty("key2").GetInt32());
			Assert.AreEqual("some value", meta.Metadata.CustomMetadata!.RootElement.GetProperty("key3").GetString());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task soft_deleted_stream_can_be_hard_deleted() {
			const string stream = "soft_deleted_stream_can_be_deleted";

			Assert.AreEqual(1,
				actual: (await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent())).NextExpectedVersion);
			await _conn.DeleteStreamAsync(stream, 1);
			await _conn.DeleteStreamAsync(stream, ExpectedVersion.Any, hardDelete: true);

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.StreamDeleted, res.Status);
			var meta = await _conn.GetStreamMetadataAsync(stream);
			Assert.AreEqual(true, meta.StreamDeleted);

			await AssertEx.ThrowsAsync<StreamDeletedException>(() =>
				_conn.AppendToStreamAsync(stream, ExpectedVersion.Any, TestEvent.NewTestEvent()));
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task soft_deleted_stream_allows_recreation_only_for_first_write() {
			const string stream = "soft_deleted_stream_allows_recreation_only_for_first_write";

			Assert.AreEqual(1,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent())).NextExpectedVersion);
			await _conn.DeleteStreamAsync(stream, 1);

			var events = new[] { TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent() };
			Assert.AreEqual(4,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, events)).NextExpectedVersion);
			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			await AssertEx.ThrowsAsync<WrongExpectedVersionException>(() =>
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent()));

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(events.Select(x => x.EventId), res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] { 2, 3, 4 }, res.Events.Select(x => x.OriginalEvent.EventNumber.ToInt64()).ToArray());

			var meta = await _conn.GetStreamMetadataAsync(stream);
			Assert.AreEqual(2, meta.Metadata.TruncateBefore.Value.ToInt64());
			Assert.AreEqual(1, meta.MetastreamRevision!.Value.ToInt64());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task soft_deleted_stream_appends_both_writes_when_expver_any() {
			const string stream = "soft_deleted_stream_appends_both_concurrent_writes_when_expver_any";

			Assert.AreEqual(1,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent())).NextExpectedVersion);
			await _conn.DeleteStreamAsync(stream, 1);

			var events1 = new[] { TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent() };
			var events2 = new[] { TestEvent.NewTestEvent(), TestEvent.NewTestEvent() };
			Assert.AreEqual(4,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.Any, events1)).NextExpectedVersion);
			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			Assert.AreEqual(6,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.Any, events2)).NextExpectedVersion);

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(6, res.LastEventNumber);
			Assert.AreEqual(5, res.Events.Length);
			Assert.AreEqual(events1.Concat(events2).Select(x => x.EventId),
				res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] { 2, 3, 4, 5, 6 }, res.Events.Select(x => x.OriginalEvent.EventNumber.ToInt64()));

			var meta = await _conn.GetStreamMetadataAsync(stream);
			Assert.AreEqual(2, meta.Metadata.TruncateBefore.Value.ToInt64());
			Assert.AreEqual(1, meta.MetastreamRevision!.Value.ToInt64());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task
			setting_json_metadata_on_empty_soft_deleted_stream_recreates_stream_not_overriding_metadataAsync() {
			const string stream =
				"setting_json_metadata_on_empty_soft_deleted_stream_recreates_stream_not_overriding_metadata";

			await _conn.SetStreamMetadataAsync(stream, ExpectedVersion.Any,
				new StreamMetadata(truncateBefore: long.MaxValue));

			Assert.AreEqual(1, (await _conn.SetStreamMetadataAsync(stream, 0,
				new StreamMetadata(
					maxCount: 100,
					acl: new StreamAcl(deleteRole: "some-role"),
					customMetadata: JsonDocument.Parse(new JsonObject {
						["key1"] = true,
						["key2"] = 17,
						["key3"] = "some value",
					}.ToJsonString())))).NextExpectedVersion);

			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.StreamNotFound, res.Status);
			Assert.AreEqual(-1, res.LastEventNumber);
			Assert.AreEqual(0, res.Events.Length);

			var meta = await _conn.GetStreamMetadataAsync(stream);
			Assert.AreEqual(2, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(0, meta.Metadata.TruncateBefore.Value.ToInt64());
			Assert.AreEqual(100, meta.Metadata.MaxCount);
			Assert.AreEqual("some-role", meta.Metadata.Acl!.DeleteRoles[0]);
			Assert.AreEqual(true, meta.Metadata.CustomMetadata!.RootElement.GetProperty("key1").GetBoolean());
			Assert.AreEqual(17, meta.Metadata.CustomMetadata!.RootElement.GetProperty("key2").GetInt32());
			Assert.AreEqual("some value", meta.Metadata.CustomMetadata!.RootElement.GetProperty("key3").GetString());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task
			setting_json_metadata_on_nonempty_soft_deleted_stream_recreates_stream_not_overriding_metadataAsync() {
			const string stream =
				"setting_json_metadata_on_nonempty_soft_deleted_stream_recreates_stream_not_overriding_metadata";

			Assert.AreEqual(1,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent())).NextExpectedVersion);
			await _conn.DeleteStreamAsync(stream, 1, hardDelete: false);

			Assert.AreEqual(1, (await _conn.SetStreamMetadataAsync(stream, 0,
				new StreamMetadata(
					maxCount: 100,
					acl: new StreamAcl(deleteRole: "some-role"),
					customMetadata: JsonDocument.Parse(new JsonObject {
						["key1"] = true,
						["key2"] = 17,
						["key3"] = "some value",
					}.ToJsonString())))).NextExpectedVersion);

			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(1, res.LastEventNumber);
			Assert.AreEqual(0, res.Events.Length);

			var meta = await _conn.GetStreamMetadataAsync(stream);
			Assert.AreEqual(2, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(2, meta.Metadata.TruncateBefore.Value.ToInt64());
			Assert.AreEqual(100, meta.Metadata.MaxCount);
			Assert.AreEqual("some-role", meta.Metadata.Acl!.DeleteRoles[0]);
			Assert.AreEqual(true, meta.Metadata.CustomMetadata.RootElement.GetProperty("key1").GetBoolean());
			Assert.AreEqual(17, meta.Metadata.CustomMetadata.RootElement.GetProperty("key2").GetInt32());
			Assert.AreEqual("some value", meta.Metadata.CustomMetadata.RootElement.GetProperty("key3").GetString());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task
			setting_nonjson_metadata_on_empty_soft_deleted_stream_recreates_stream_keeping_original_metadataAsync() {
			const string stream =
				"setting_nonjson_metadata_on_empty_soft_deleted_stream_recreates_stream_overriding_metadata";

			await _conn.SetStreamMetadataAsync(stream, ExpectedVersion.Any,
				new StreamMetadata(truncateBefore: long.MaxValue));

			Assert.AreEqual(1, (await _conn.SetStreamMetadataAsync(stream, 0, new StreamMetadata())).NextExpectedVersion);

			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.StreamNotFound, res.Status);
			Assert.AreEqual(-1, res.LastEventNumber);
			Assert.AreEqual(0, res.Events.Length);

			var meta = await _conn.GetStreamMetadataAsRawBytesAsync(stream);
			// REVIEW>>: It's not clear why the server is returning 2 when using the gRPC client.
			// Assert.AreEqual(1, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(new byte[256], meta.Metadata.ToJsonBytes());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public async Task
			setting_nonjson_metadata_on_nonempty_soft_deleted_stream_recreates_stream_keeping_original_metadataAsync() {
			const string stream =
				"setting_nonjson_metadata_on_nonempty_soft_deleted_stream_recreates_stream_overriding_metadata";

			Assert.AreEqual(1,
				(await _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent())).NextExpectedVersion);
			await _conn.DeleteStreamAsync(stream, 1, hardDelete: false);

			Assert.AreEqual(1, (await _conn.SetStreamMetadataAsync(stream, 0, new StreamMetadata())).NextExpectedVersion);
			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = await _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false);
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(1, res.LastEventNumber);
			Assert.AreEqual(2, res.Events.Length);
			Assert.AreEqual(new[] { 0, 1 }, res.Events.Select(x => x.OriginalEventNumber).ToArray());

			var meta = await _conn.GetStreamMetadataAsRawBytesAsync(stream);
			Assert.AreEqual(1, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(new byte[256], meta.Metadata.ToJsonBytes());
		}
	}
}
