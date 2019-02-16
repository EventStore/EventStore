using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class when_committing_empty_transaction : SpecificationWithDirectory {
		private MiniNode _node;
		private IEventStoreConnection _connection;
		private EventData _firstEvent;

		[SetUp]
		public override void SetUp() {
			base.SetUp();
			_node = new MiniNode(PathName);
			_node.Start();

			_firstEvent = TestEvent.NewTestEvent();

			_connection = BuildConnection(_node);
			_connection.ConnectAsync().Wait();

			Assert.AreEqual(2, _connection.AppendToStreamAsync("test-stream",
				ExpectedVersion.NoStream,
				_firstEvent,
				TestEvent.NewTestEvent(),
				TestEvent.NewTestEvent()).Result.NextExpectedVersion);

			using (var transaction = _connection.StartTransactionAsync("test-stream", 2).Result) {
				Assert.AreEqual(2, transaction.CommitAsync().Result.NextExpectedVersion);
			}
		}

		[TearDown]
		public override void TearDown() {
			_connection.Close();
			_node.Shutdown();
			base.TearDown();
		}

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.Create(node.TcpEndPoint);
		}

		[Test]
		public void following_append_with_correct_expected_version_are_commited_correctly() {
			Assert.AreEqual(4,
				_connection.AppendToStreamAsync("test-stream", 2, TestEvent.NewTestEvent(), TestEvent.NewTestEvent())
					.Result.NextExpectedVersion);

			var res = _connection.ReadStreamEventsForwardAsync("test-stream", 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(5, res.Events.Length);
			for (int i = 0; i < 5; ++i) {
				Assert.AreEqual(i, res.Events[i].OriginalEventNumber);
			}
		}

		[Test]
		public void following_append_with_expected_version_any_are_commited_correctly() {
			Assert.AreEqual(4,
				_connection.AppendToStreamAsync("test-stream", ExpectedVersion.Any, TestEvent.NewTestEvent(),
					TestEvent.NewTestEvent()).Result.NextExpectedVersion);

			var res = _connection.ReadStreamEventsForwardAsync("test-stream", 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(5, res.Events.Length);
			for (int i = 0; i < 5; ++i) {
				Assert.AreEqual(i, res.Events[i].OriginalEventNumber);
			}
		}

		[Test]
		public void committing_first_event_with_expected_version_no_stream_is_idempotent() {
			Assert.AreEqual(0,
				_connection.AppendToStreamAsync("test-stream", ExpectedVersion.NoStream, _firstEvent).Result
					.NextExpectedVersion);

			var res = _connection.ReadStreamEventsForwardAsync("test-stream", 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			for (int i = 0; i < 3; ++i) {
				Assert.AreEqual(i, res.Events[i].OriginalEventNumber);
			}
		}

		[Test]
		public void trying_to_append_new_events_with_expected_version_no_stream_fails() {
			Assert.That(
				() => _connection.AppendToStreamAsync("test-stream", ExpectedVersion.NoStream, TestEvent.NewTestEvent())
					.Result,
				Throws.Exception.InstanceOf<AggregateException>()
					.With.InnerException.InstanceOf<WrongExpectedVersionException>());
		}
	}
}
