using System.Linq;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class when_having_max_count_set_for_stream : SpecificationWithDirectory {
		private const string Stream = "max-count-test-stream";

		private MiniNode _node;
		private IEventStoreConnection _connection;
		private EventData[] _testEvents;

		[SetUp]
		public override void SetUp() {
			base.SetUp();
			_node = new MiniNode(PathName);
			_node.Start();

			_connection = TestConnection.Create(_node.TcpEndPoint);
			_connection.ConnectAsync().Wait();

			_connection.SetStreamMetadataAsync(Stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetMaxCount(3)).Wait();

			_testEvents = Enumerable.Range(0, 5).Select(x => TestEvent.NewTestEvent(data: x.ToString())).ToArray();
			_connection.AppendToStreamAsync(Stream, ExpectedVersion.EmptyStream, _testEvents).Wait();
		}

		[TearDown]
		public override void TearDown() {
			_connection.Close();
			_node.Shutdown();
			base.TearDown();
		}

		[Test]
		public void read_stream_forward_respects_max_count() {
			var res = _connection.ReadStreamEventsForwardAsync(Stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());
		}

		[Test]
		public void read_stream_backward_respects_max_count() {
			var res = _connection.ReadStreamEventsBackwardAsync(Stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
		}

		[Test]
		public void after_setting_less_strict_max_count_read_stream_forward_reads_more_events() {
			var res = _connection.ReadStreamEventsForwardAsync(Stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());

			_connection.SetStreamMetadataAsync(Stream, 0, StreamMetadata.Build().SetMaxCount(4)).Wait();

			res = _connection.ReadStreamEventsForwardAsync(Stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(1).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());
		}

		[Test]
		public void after_setting_more_strict_max_count_read_stream_forward_reads_less_events() {
			var res = _connection.ReadStreamEventsForwardAsync(Stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());

			_connection.SetStreamMetadataAsync(Stream, 0, StreamMetadata.Build().SetMaxCount(2)).Wait();

			res = _connection.ReadStreamEventsForwardAsync(Stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(2, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());
		}

		[Test]
		public void after_setting_less_strict_max_count_read_stream_backward_reads_more_events() {
			var res = _connection.ReadStreamEventsBackwardAsync(Stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

			_connection.SetStreamMetadataAsync(Stream, 0, StreamMetadata.Build().SetMaxCount(4)).Wait();

			res = _connection.ReadStreamEventsBackwardAsync(Stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(1).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
		}

		[Test]
		public void after_setting_more_strict_max_count_read_stream_backward_reads_less_events() {
			var res = _connection.ReadStreamEventsBackwardAsync(Stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

			_connection.SetStreamMetadataAsync(Stream, 0, StreamMetadata.Build().SetMaxCount(2)).Wait();

			res = _connection.ReadStreamEventsBackwardAsync(Stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(2, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
		}
	}
}
