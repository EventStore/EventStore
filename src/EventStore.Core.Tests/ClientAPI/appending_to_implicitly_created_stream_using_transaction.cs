using System;
using System.Linq;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class appending_to_implicitly_created_stream_using_transaction : SpecificationWithDirectoryPerTestFixture {
		private MiniNode _node;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_node = new MiniNode(PathName);
			_node.Start();
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_node.Shutdown();
			base.TestFixtureTearDown();
		}

		virtual protected IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.Create(node.TcpEndPoint);
		}

		/*
		 * sequence - events written so stream
		 * 0em1 - event number 0 written with exp version -1 (minus 1)
		 * 1any - event number 1 written with exp version any
		 * S_0em1_1em1_E - START bucket, two events in bucket, END bucket
		*/

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_3e2_4e3_5e4_0em1_idempotent() {
			const string stream =
				"appending_to_implicitly_created_stream_using_transaction_sequence_0em1_1e0_2e1_3e2_4e3_5e4_0em1_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new TransactionalWriter(store, stream);

				Assert.AreEqual(5, writer.StartTransaction(-1).Write(events).Commit().NextExpectedVersion);
				Assert.AreEqual(0, writer.StartTransaction(-1).Write(events.First()).Commit().NextExpectedVersion);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_3e2_4e3_5e4_0any_idempotent() {
			const string stream =
				"appending_to_implicitly_created_stream_using_transaction_sequence_0em1_1e0_2e1_3e2_4e3_5e4_0any_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new TransactionalWriter(store, stream);

				Assert.AreEqual(5, writer.StartTransaction(-1).Write(events).Commit().NextExpectedVersion);
				Assert.AreEqual(0,
					writer.StartTransaction(ExpectedVersion.Any).Write(events.First()).Commit().NextExpectedVersion);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e5_non_idempotent() {
			const string stream =
				"appending_to_implicitly_created_stream_using_transaction_sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e5_non_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new TransactionalWriter(store, stream);

				Assert.AreEqual(5, writer.StartTransaction(-1).Write(events).Commit().NextExpectedVersion);
				Assert.AreEqual(6, writer.StartTransaction(5).Write(events.First()).Commit().NextExpectedVersion);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length + 1));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e6_wev() {
			const string stream =
				"appending_to_implicitly_created_stream_using_transaction_sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e6_wev";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new TransactionalWriter(store, stream);

				Assert.AreEqual(5, writer.StartTransaction(-1).Write(events).Commit().NextExpectedVersion);
				Assert.That(() => writer.StartTransaction(6).Write(events.First()).Commit(),
					Throws.Exception.TypeOf<AggregateException>().With.InnerException
						.TypeOf<WrongExpectedVersionException>());
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e4_wev() {
			const string stream =
				"appending_to_implicitly_created_stream_using_transaction_sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e4_wev";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new TransactionalWriter(store, stream);

				Assert.AreEqual(5, writer.StartTransaction(-1).Write(events).Commit().NextExpectedVersion);
				Assert.That(() => writer.StartTransaction(4).Write(events.First()).Commit(),
					Throws.Exception.TypeOf<AggregateException>().With.InnerException
						.TypeOf<WrongExpectedVersionException>());
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_0e0_non_idempotent() {
			const string stream =
				"appending_to_implicitly_created_stream_using_transaction_sequence_0em1_0e0_non_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 1).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new TransactionalWriter(store, stream);

				Assert.AreEqual(0, writer.StartTransaction(-1).Write(events).Commit().NextExpectedVersion);
				Assert.AreEqual(1, writer.StartTransaction(0).Write(events.First()).Commit().NextExpectedVersion);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length + 1));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_0any_idempotent() {
			const string stream =
				"appending_to_implicitly_created_stream_using_transaction_sequence_0em1_0any_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 1).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new TransactionalWriter(store, stream);

				Assert.AreEqual(0, writer.StartTransaction(-1).Write(events).Commit().NextExpectedVersion);
				Assert.AreEqual(0,
					writer.StartTransaction(ExpectedVersion.Any).Write(events.First()).Commit().NextExpectedVersion);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_0em1_idempotent() {
			const string stream =
				"appending_to_implicitly_created_stream_using_transaction_sequence_0em1_0em1_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 1).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new TransactionalWriter(store, stream);

				Assert.AreEqual(0, writer.StartTransaction(-1).Write(events).Commit().NextExpectedVersion);
				Assert.AreEqual(0, writer.StartTransaction(-1).Write(events.First()).Commit().NextExpectedVersion);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_1any_1any_idempotent() {
			const string stream =
				"appending_to_implicitly_created_stream_using_transaction_sequence_0em1_1e0_2e1_1any_1any_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 3).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new TransactionalWriter(store, stream);

				Assert.AreEqual(2, writer.StartTransaction(-1).Write(events).Commit().NextExpectedVersion);
				Assert.AreEqual(1,
					writer.StartTransaction(ExpectedVersion.Any).Write(events[1]).Write(events[1]).Commit()
						.NextExpectedVersion);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_S_0em1_1em1_E_S_0em1_1em1_2em1_E_idempotancy_fail() {
			const string stream =
				"appending_to_implicitly_created_stream_using_transaction_sequence_S_0em1_1em1_E_S_0em1_1em1_2em1_E_idempotancy_fail";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 2).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new TransactionalWriter(store, stream);

				Assert.AreEqual(1, writer.StartTransaction(-1).Write(events).Commit().NextExpectedVersion);
				Assert.That(() => writer.StartTransaction(-1)
						.Write(events.Concat(new[] {TestEvent.NewTestEvent(Guid.NewGuid())}).ToArray())
						.Commit(),
					Throws.Exception.TypeOf<AggregateException>().With.InnerException
						.TypeOf<WrongExpectedVersionException>());
			}
		}
	}
}
