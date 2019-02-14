using System;
using System.Linq;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class appending_to_implicitly_created_stream : SpecificationWithDirectoryPerTestFixture {
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


		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
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
				"appending_to_implicitly_created_stream_sequence_0em1_1e0_2e1_3e2_4e3_5e4_0em1_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new StreamWriter(store, stream, -1);

				Assert.DoesNotThrow(() => writer.Append(events).Then(events.First(), -1));

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_3e2_4e3_4e4_0any_idempotent() {
			const string stream =
				"appending_to_implicitly_created_stream_sequence_0em1_1e0_2e1_3e2_4e3_4e4_0any_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new StreamWriter(store, stream, -1);

				Assert.DoesNotThrow(() => writer.Append(events).Then(events.First(), ExpectedVersion.Any));

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e5_non_idempotent() {
			const string stream =
				"appending_to_implicitly_created_stream_sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e5_non_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new StreamWriter(store, stream, -1);

				Assert.DoesNotThrow(() => writer.Append(events).Then(events.First(), 5));

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length + 1));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e6_wev() {
			const string stream = "appending_to_implicitly_created_stream_sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e6_wev";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new StreamWriter(store, stream, -1);

				var first6 = writer.Append(events);
				Assert.That(() => first6.Then(events.First(), 6),
					Throws.Exception.TypeOf<AggregateException>().With.InnerException
						.TypeOf<WrongExpectedVersionException>());
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e4_wev() {
			const string stream = "appending_to_implicitly_created_stream_sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e4_wev";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 6).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new StreamWriter(store, stream, -1);

				var first6 = writer.Append(events);
				Assert.That(() => first6.Then(events.First(), 4),
					Throws.Exception.TypeOf<AggregateException>().With.InnerException
						.TypeOf<WrongExpectedVersionException>());
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_0e0_non_idempotent() {
			const string stream = "appending_to_implicitly_created_stream_sequence_0em1_0e0_non_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 1).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new StreamWriter(store, stream, -1);

				Assert.DoesNotThrow(() => writer.Append(events).Then(events.First(), 0));

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length + 1));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_0any_idempotent() {
			const string stream = "appending_to_implicitly_created_stream_sequence_0em1_0any_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 1).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new StreamWriter(store, stream, -1);

				Assert.DoesNotThrow(() => writer.Append(events).Then(events.First(), ExpectedVersion.Any));

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_0em1_idempotent() {
			const string stream = "appending_to_implicitly_created_stream_sequence_0em1_0em1_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 1).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new StreamWriter(store, stream, -1);

				Assert.DoesNotThrow(() => writer.Append(events).Then(events.First(), -1));

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_0em1_1e0_2e1_1any_1any_idempotent() {
			const string stream = "appending_to_implicitly_created_stream_sequence_0em1_1e0_2e1_1any_1any_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 3).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var writer = new StreamWriter(store, stream, -1);

				Assert.DoesNotThrow(() =>
					writer.Append(events).Then(events[1], ExpectedVersion.Any).Then(events[1], ExpectedVersion.Any));

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_S_0em1_1em1_E_S_0em1_E_idempotent() {
			const string stream = "appending_to_implicitly_created_stream_sequence_S_0em1_1em1_E_S_0em1_E_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 2).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var append = store.AppendToStreamAsync(stream, -1, events);
				Assert.DoesNotThrow(append.Wait);

				var app2 = store.AppendToStreamAsync(stream, -1, new[] {events.First()});
				Assert.DoesNotThrow(app2.Wait);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_S_0em1_1em1_E_S_0any_E_idempotent() {
			const string stream = "appending_to_implicitly_created_stream_sequence_S_0em1_1em1_E_S_0any_E_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 2).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var append = store.AppendToStreamAsync(stream, -1, events);
				Assert.DoesNotThrow(append.Wait);

				var app2 = store.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] {events.First()});
				Assert.DoesNotThrow(app2.Wait);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_S_0em1_1em1_E_S_1e0_E_idempotent() {
			const string stream = "appending_to_implicitly_created_stream_sequence_S_0em1_1em1_E_S_1e0_E_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 2).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var append = store.AppendToStreamAsync(stream, -1, events);
				Assert.DoesNotThrow(append.Wait);

				var app2 = store.AppendToStreamAsync(stream, 0, new[] {events[1]});
				Assert.DoesNotThrow(app2.Wait);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_S_0em1_1em1_E_S_1any_E_idempotent() {
			const string stream = "appending_to_implicitly_created_stream_sequence_S_0em1_1em1_E_S_1any_E_idempotent";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 2).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var append = store.AppendToStreamAsync(stream, -1, events);
				Assert.DoesNotThrow(append.Wait);

				var app2 = store.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] {events[1]});
				Assert.DoesNotThrow(app2.Wait);

				var total = EventsStream.Count(store, stream);
				Assert.That(total, Is.EqualTo(events.Length));
			}
		}

		[Test]
		[Category("Network")]
		public void sequence_S_0em1_1em1_E_S_0em1_1em1_2em1_E_idempotancy_fail() {
			const string stream =
				"appending_to_implicitly_created_stream_sequence_S_0em1_1em1_E_S_0em1_1em1_2em1_E_idempotancy_fail";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = Enumerable.Range(0, 2).Select(x => TestEvent.NewTestEvent(Guid.NewGuid())).ToArray();
				var append = store.AppendToStreamAsync(stream, -1, events);
				Assert.DoesNotThrow(append.Wait);

				var app2 = store.AppendToStreamAsync(stream, -1,
					events.Concat(new[] {TestEvent.NewTestEvent(Guid.NewGuid())}));
				Assert.That(() => app2.Wait(),
					Throws.Exception.TypeOf<AggregateException>().With.InnerException
						.TypeOf<WrongExpectedVersionException>());
			}
		}
	}
}
