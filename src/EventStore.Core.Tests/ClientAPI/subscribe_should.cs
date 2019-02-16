using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class subscribe_should : SpecificationWithDirectoryPerTestFixture {
		private const int Timeout = 10000;

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

		[Test, Category("LongRunning")]
		public void be_able_to_subscribe_to_non_existing_stream_and_then_catch_new_event() {
			const string stream =
				"subscribe_should_be_able_to_subscribe_to_non_existing_stream_and_then_catch_created_event";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new CountdownEvent(1);
				var dropped = new CountdownEvent(1);

				using (store.SubscribeToStreamAsync(stream, false, (s, x) => {
					appeared.Signal();
					return Task.CompletedTask;
				}, (s, r, e) => dropped.Signal()).Result) {
					store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).Wait();
					Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event timed out.");
				}
			}
		}

		[Test, Category("LongRunning")]
		public void allow_multiple_subscriptions_to_same_stream() {
			const string stream = "subscribe_should_allow_multiple_subscriptions_to_same_stream";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new CountdownEvent(2);
				var dropped = new CountdownEvent(2);

				using (store.SubscribeToStreamAsync(stream, false, (s, x) => {
					appeared.Signal();
					return Task.CompletedTask;
				}, (s, r, e) => dropped.Signal()).Result)
				using (store.SubscribeToStreamAsync(stream, false, (s, x) => {
					appeared.Signal();
					return Task.CompletedTask;
				}, (s, r, e) => dropped.Signal()).Result) {
					store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).Wait();
					Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event timed out.");
				}
			}
		}

		[Test, Category("LongRunning")]
		public void call_dropped_callback_after_unsubscribe_method_call() {
			const string stream = "subscribe_should_call_dropped_callback_after_unsubscribe_method_call";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var dropped = new CountdownEvent(1);
				using (var subscription = store.SubscribeToStreamAsync(stream, false, (s, x) => Task.CompletedTask,
					(s, r, e) => dropped.Signal()).Result) {
					subscription.Unsubscribe();
				}

				Assert.IsTrue(dropped.Wait(Timeout), "Dropped countdown event timed out.");
			}
		}

		[Test, Category("LongRunning")]
		public void catch_deleted_events_as_well() {
			const string stream = "subscribe_should_catch_created_and_deleted_events_as_well";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var appeared = new CountdownEvent(1);
				var dropped = new CountdownEvent(1);
				using (store.SubscribeToStreamAsync(stream, false, (s, x) => {
						appeared.Signal();
						return Task.CompletedTask;
					},
					(s, r, e) => dropped.Signal()).Result) {
					store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true).Wait();
					Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event timed out.");
				}
			}
		}
	}
}
