using System;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class soft_delete : SpecificationWithDirectoryPerTestFixture {
		private MiniNode _node;
		private IEventStoreConnection _conn;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_node = new MiniNode(PathName);
			_node.Start();

			_conn = BuildConnection(_node);
			_conn.ConnectAsync().Wait();
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_conn.Close();
			_node.Shutdown();
			base.TestFixtureTearDown();
		}

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return EventStoreConnection.Create(node.TcpEndPoint.ToESTcpUri());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void soft_deleted_stream_returns_no_stream_and_no_events_on_read() {
			const string stream = "soft_deleted_stream_returns_no_stream_and_no_events_on_read";

			Assert.AreEqual(1,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);
			_conn.DeleteStreamAsync(stream, 1).Wait();

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.StreamNotFound, res.Status);
			Assert.AreEqual(0, res.Events.Length);
			Assert.AreEqual(1, res.LastEventNumber);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void soft_deleted_stream_allows_recreation_when_expver_any() {
			const string stream = "soft_deleted_stream_allows_recreation_when_expver_any";

			Assert.AreEqual(1,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);
			_conn.DeleteStreamAsync(stream, 1).Wait();

			var events = new[] {TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent()};
			Assert.AreEqual(4,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.Any, events).Result.NextExpectedVersion);

			Thread.Sleep(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(events.Select(x => x.EventId), res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] {2, 3, 4}, res.Events.Select(x => x.OriginalEvent.EventNumber));

			var meta = _conn.GetStreamMetadataAsync(stream).Result;
			Assert.AreEqual(2, meta.StreamMetadata.TruncateBefore);
			Assert.AreEqual(1, meta.MetastreamVersion);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void soft_deleted_stream_allows_recreation_when_expver_no_stream() {
			const string stream = "soft_deleted_stream_allows_recreation_when_expver_no_stream";

			Assert.AreEqual(1,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);
			_conn.DeleteStreamAsync(stream, 1).Wait();

			var events = new[] {TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent()};
			Assert.AreEqual(4,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, events).Result.NextExpectedVersion);

			Thread.Sleep(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(events.Select(x => x.EventId), res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] {2, 3, 4}, res.Events.Select(x => x.OriginalEvent.EventNumber));

			var meta = _conn.GetStreamMetadataAsync(stream).Result;
			Assert.AreEqual(2, meta.StreamMetadata.TruncateBefore);
			Assert.AreEqual(1, meta.MetastreamVersion);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void soft_deleted_stream_allows_recreation_when_expver_is_exact() {
			const string stream = "soft_deleted_stream_allows_recreation_when_expver_is_exact";

			Assert.AreEqual(1,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);
			_conn.DeleteStreamAsync(stream, 1).Wait();

			var events = new[] {TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent()};
			Assert.AreEqual(4, _conn.AppendToStreamAsync(stream, 1, events).Result.NextExpectedVersion);

			Thread.Sleep(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(events.Select(x => x.EventId), res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] {2, 3, 4}, res.Events.Select(x => x.OriginalEvent.EventNumber));

			var meta = _conn.GetStreamMetadataAsync(stream).Result;
			Assert.AreEqual(2, meta.StreamMetadata.TruncateBefore);
			Assert.AreEqual(1, meta.MetastreamVersion);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void soft_deleted_stream_when_recreated_preserves_metadata_except_truncatebefore() {
			const string stream = "soft_deleted_stream_when_recreated_preserves_metadata_except_truncatebefore";

			Assert.AreEqual(1,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);

			Assert.AreEqual(0, _conn.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream,
				StreamMetadata.Build().SetTruncateBefore(long.MaxValue)
					.SetMaxCount(100)
					.SetDeleteRole("some-role")
					.SetCustomProperty("key1", true)
					.SetCustomProperty("key2", 17)
					.SetCustomProperty("key3", "some value")).Result.NextExpectedVersion);

			var events = new[] {TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent()};
			Assert.AreEqual(4, _conn.AppendToStreamAsync(stream, 1, events).Result.NextExpectedVersion);
			Thread.Sleep(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(events.Select(x => x.EventId), res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] {2, 3, 4}, res.Events.Select(x => x.OriginalEvent.EventNumber));

			var meta = _conn.GetStreamMetadataAsync(stream).Result;
			Assert.AreEqual(1, meta.MetastreamVersion);
			Assert.AreEqual(2, meta.StreamMetadata.TruncateBefore);
			Assert.AreEqual(100, meta.StreamMetadata.MaxCount);
			Assert.AreEqual("some-role", meta.StreamMetadata.Acl.DeleteRole);
			Assert.AreEqual(true, meta.StreamMetadata.GetValue<bool>("key1"));
			Assert.AreEqual(17, meta.StreamMetadata.GetValue<int>("key2"));
			Assert.AreEqual("some value", meta.StreamMetadata.GetValue<string>("key3"));
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void soft_deleted_stream_can_be_hard_deleted() {
			const string stream = "soft_deleted_stream_can_be_deleted";

			Assert.AreEqual(1,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);
			_conn.DeleteStreamAsync(stream, 1).Wait();
			_conn.DeleteStreamAsync(stream, ExpectedVersion.Any, hardDelete: true).Wait();

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.StreamDeleted, res.Status);
			var meta = _conn.GetStreamMetadataAsync(stream).Result;
			Assert.AreEqual(true, meta.IsStreamDeleted);

			Assert.That(() => _conn.AppendToStreamAsync(stream, ExpectedVersion.Any, TestEvent.NewTestEvent()).Wait(),
				Throws.Exception.InstanceOf<AggregateException>()
					.With.InnerException.InstanceOf<StreamDeletedException>());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void soft_deleted_stream_allows_recreation_only_for_first_write() {
			const string stream = "soft_deleted_stream_allows_recreation_only_for_first_write";

			Assert.AreEqual(1,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);
			_conn.DeleteStreamAsync(stream, 1).Wait();

			var events = new[] {TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent()};
			Assert.AreEqual(4,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, events).Result.NextExpectedVersion);
			Thread.Sleep(50); //TODO: This is a workaround until github issue #1744 is fixed

			Assert.That(
				() => _conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent()).Wait(),
				Throws.Exception.InstanceOf<AggregateException>()
					.With.InnerException.InstanceOf<WrongExpectedVersionException>());

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(events.Select(x => x.EventId), res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] {2, 3, 4}, res.Events.Select(x => x.OriginalEvent.EventNumber));

			var meta = _conn.GetStreamMetadataAsync(stream).Result;
			Assert.AreEqual(2, meta.StreamMetadata.TruncateBefore);
			Assert.AreEqual(1, meta.MetastreamVersion);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void soft_deleted_stream_appends_both_writes_when_expver_any() {
			const string stream = "soft_deleted_stream_appends_both_concurrent_writes_when_expver_any";

			Assert.AreEqual(1,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);
			_conn.DeleteStreamAsync(stream, 1).Wait();

			var events1 = new[] {TestEvent.NewTestEvent(), TestEvent.NewTestEvent(), TestEvent.NewTestEvent()};
			var events2 = new[] {TestEvent.NewTestEvent(), TestEvent.NewTestEvent()};
			Assert.AreEqual(4,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.Any, events1).Result.NextExpectedVersion);
			Thread.Sleep(50); //TODO: This is a workaround until github issue #1744 is fixed

			Assert.AreEqual(6,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.Any, events2).Result.NextExpectedVersion);

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(6, res.LastEventNumber);
			Assert.AreEqual(5, res.Events.Length);
			Assert.AreEqual(events1.Concat(events2).Select(x => x.EventId),
				res.Events.Select(x => x.OriginalEvent.EventId));
			Assert.AreEqual(new[] {2, 3, 4, 5, 6}, res.Events.Select(x => x.OriginalEvent.EventNumber));

			var meta = _conn.GetStreamMetadataAsync(stream).Result;
			Assert.AreEqual(2, meta.StreamMetadata.TruncateBefore);
			Assert.AreEqual(1, meta.MetastreamVersion);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void setting_json_metadata_on_empty_soft_deleted_stream_recreates_stream_not_overriding_metadata() {
			const string stream =
				"setting_json_metadata_on_empty_soft_deleted_stream_recreates_stream_not_overriding_metadata";

			_conn.DeleteStreamAsync(stream, ExpectedVersion.NoStream, hardDelete: false).Wait();

			Assert.AreEqual(1, _conn.SetStreamMetadataAsync(stream, 0,
				StreamMetadata.Build().SetMaxCount(100)
					.SetDeleteRole("some-role")
					.SetCustomProperty("key1", true)
					.SetCustomProperty("key2", 17)
					.SetCustomProperty("key3", "some value")).Result.NextExpectedVersion);

			Thread.Sleep(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.StreamNotFound, res.Status);
			Assert.AreEqual(-1, res.LastEventNumber);
			Assert.AreEqual(0, res.Events.Length);

			var meta = _conn.GetStreamMetadataAsync(stream).Result;
			Assert.AreEqual(2, meta.MetastreamVersion);
			Assert.AreEqual(0, meta.StreamMetadata.TruncateBefore);
			Assert.AreEqual(100, meta.StreamMetadata.MaxCount);
			Assert.AreEqual("some-role", meta.StreamMetadata.Acl.DeleteRole);
			Assert.AreEqual(true, meta.StreamMetadata.GetValue<bool>("key1"));
			Assert.AreEqual(17, meta.StreamMetadata.GetValue<int>("key2"));
			Assert.AreEqual("some value", meta.StreamMetadata.GetValue<string>("key3"));
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void setting_json_metadata_on_nonempty_soft_deleted_stream_recreates_stream_not_overriding_metadata() {
			const string stream =
				"setting_json_metadata_on_nonempty_soft_deleted_stream_recreates_stream_not_overriding_metadata";

			Assert.AreEqual(1,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);
			_conn.DeleteStreamAsync(stream, 1, hardDelete: false).Wait();

			Assert.AreEqual(1, _conn.SetStreamMetadataAsync(stream, 0,
				StreamMetadata.Build().SetMaxCount(100)
					.SetDeleteRole("some-role")
					.SetCustomProperty("key1", true)
					.SetCustomProperty("key2", 17)
					.SetCustomProperty("key3", "some value")).Result.NextExpectedVersion);

			Thread.Sleep(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(1, res.LastEventNumber);
			Assert.AreEqual(0, res.Events.Length);

			var meta = _conn.GetStreamMetadataAsync(stream).Result;
			Assert.AreEqual(2, meta.MetastreamVersion);
			Assert.AreEqual(2, meta.StreamMetadata.TruncateBefore);
			Assert.AreEqual(100, meta.StreamMetadata.MaxCount);
			Assert.AreEqual("some-role", meta.StreamMetadata.Acl.DeleteRole);
			Assert.AreEqual(true, meta.StreamMetadata.GetValue<bool>("key1"));
			Assert.AreEqual(17, meta.StreamMetadata.GetValue<int>("key2"));
			Assert.AreEqual("some value", meta.StreamMetadata.GetValue<string>("key3"));
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void setting_nonjson_metadata_on_empty_soft_deleted_stream_recreates_stream_keeping_original_metadata() {
			const string stream =
				"setting_nonjson_metadata_on_empty_soft_deleted_stream_recreates_stream_overriding_metadata";

			_conn.DeleteStreamAsync(stream, ExpectedVersion.NoStream, hardDelete: false).Wait();

			Assert.AreEqual(1, _conn.SetStreamMetadataAsync(stream, 0, new byte[256]).Result.NextExpectedVersion);

			Thread.Sleep(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.StreamNotFound, res.Status);
			Assert.AreEqual(-1, res.LastEventNumber);
			Assert.AreEqual(0, res.Events.Length);

			var meta = _conn.GetStreamMetadataAsRawBytesAsync(stream).Result;
			Assert.AreEqual(1, meta.MetastreamVersion);
			Assert.AreEqual(new byte[256], meta.StreamMetadata);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void
			setting_nonjson_metadata_on_nonempty_soft_deleted_stream_recreates_stream_keeping_original_metadata() {
			const string stream =
				"setting_nonjson_metadata_on_nonempty_soft_deleted_stream_recreates_stream_overriding_metadata";

			Assert.AreEqual(1,
				_conn.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);
			_conn.DeleteStreamAsync(stream, 1, hardDelete: false).Wait();

			Assert.AreEqual(1, _conn.SetStreamMetadataAsync(stream, 0, new byte[256]).Result.NextExpectedVersion);
			Thread.Sleep(50); //TODO: This is a workaround until github issue #1744 is fixed

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(1, res.LastEventNumber);
			Assert.AreEqual(2, res.Events.Length);
			Assert.AreEqual(new[] {0, 1}, res.Events.Select(x => x.OriginalEventNumber).ToArray());

			var meta = _conn.GetStreamMetadataAsRawBytesAsync(stream).Result;
			Assert.AreEqual(1, meta.MetastreamVersion);
			Assert.AreEqual(new byte[256], meta.StreamMetadata);
		}
	}
}
