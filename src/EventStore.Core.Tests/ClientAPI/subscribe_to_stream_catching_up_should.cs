using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Common.Log;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class subscribe_to_stream_catching_up_should : SpecificationWithDirectoryPerTestFixture {
		private static readonly EventStore.Common.Log.ILogger Log =
			LogManager.GetLoggerFor<subscribe_to_stream_catching_up_should>();

		private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(500);

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

		[Test, Category("LongRunning")]
		public void be_able_to_subscribe_to_non_existing_stream() {
			const string stream = "be_able_to_subscribe_to_non_existing_stream";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new ManualResetEventSlim(false);
				var dropped = new CountdownEvent(1);

				var subscription = store.SubscribeToStreamFrom(stream,
					null,
					CatchUpSubscriptionSettings.Default,
					(_, x) => {
						appeared.Set();
						return Task.CompletedTask;
					},
					_ => Log.Info("Live processing started."),
					(_, __, ___) => dropped.Signal());

				Thread.Sleep(100); // give time for first pull phase
				store.SubscribeToStreamAsync(stream, false, (s, x) => Task.CompletedTask, (s, r, e) => { }).Wait();
				Thread.Sleep(100);
				Assert.IsFalse(appeared.Wait(0), "Some event appeared.");
				Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
				subscription.Stop(Timeout);
				Assert.IsTrue(dropped.Wait(Timeout));
			}
		}

		[Test, Category("LongRunning")]
		public void be_able_to_subscribe_to_non_existing_stream_and_then_catch_event() {
			const string stream = "be_able_to_subscribe_to_non_existing_stream_and_then_catch_event";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new CountdownEvent(1);
				var dropped = new CountdownEvent(1);

				var subscription = store.SubscribeToStreamFrom(stream,
					null,
					CatchUpSubscriptionSettings.Default,
					(_, x) => {
						appeared.Signal();
						return Task.CompletedTask;
					},
					_ => Log.Info("Live processing started."),
					(_, __, ___) => dropped.Signal());

				store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).Wait();

				if (!appeared.Wait(Timeout)) {
					Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
					Assert.Fail("Appeared countdown event timed out.");
				}

				Assert.IsFalse(dropped.Wait(0));
				subscription.Stop(Timeout);
				Assert.IsTrue(dropped.Wait(Timeout));
			}
		}

		[Test, Category("LongRunning")]
		public void allow_multiple_subscriptions_to_same_stream() {
			const string stream = "allow_multiple_subscriptions_to_same_stream";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new CountdownEvent(2);
				var dropped1 = new ManualResetEventSlim(false);
				var dropped2 = new ManualResetEventSlim(false);

				var sub1 = store.SubscribeToStreamFrom(stream,
					null,
					CatchUpSubscriptionSettings.Default,
					(_, e) => {
						appeared.Signal();
						return Task.CompletedTask;
					},
					_ => Log.Info("Live processing started."),
					(x, y, z) => dropped1.Set());
				var sub2 = store.SubscribeToStreamFrom(stream,
					null,
					CatchUpSubscriptionSettings.Default,
					(_, e) => {
						appeared.Signal();
						return Task.CompletedTask;
					},
					_ => Log.Info("Live processing started."),
					(x, y, z) => dropped2.Set());

				store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).Wait();

				if (!appeared.Wait(Timeout)) {
					Assert.IsFalse(dropped1.Wait(0), "Subscription1 was dropped prematurely.");
					Assert.IsFalse(dropped2.Wait(0), "Subscription2 was dropped prematurely.");
					Assert.Fail("Could not wait for all events.");
				}

				Assert.IsFalse(dropped1.Wait(0));
				sub1.Stop(Timeout);
				Assert.IsTrue(dropped1.Wait(Timeout));

				Assert.IsFalse(dropped2.Wait(0));
				sub2.Stop(Timeout);
				Assert.IsTrue(dropped2.Wait(Timeout));
			}
		}

		[Test, Category("LongRunning")]
		public void call_dropped_callback_after_stop_method_call() {
			const string stream = "call_dropped_callback_after_stop_method_call";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var dropped = new CountdownEvent(1);
				var subscription = store.SubscribeToStreamFrom(stream,
					null,
					CatchUpSubscriptionSettings.Default,
					(x, y) => Task.CompletedTask,
					_ => Log.Info("Live processing started."),
					(x, y, z) => dropped.Signal());
				Assert.IsFalse(dropped.Wait(0));
				subscription.Stop(Timeout);
				Assert.IsTrue(dropped.Wait(Timeout));
			}
		}

		[Test, Category("LongRunning")]
		public void call_dropped_callback_when_an_error_occurs_while_processing_an_event() {
			const string stream = "call_dropped_callback_when_an_error_occurs_while_processing_an_event";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				store.AppendToStreamAsync(stream, ExpectedVersion.Any,
					new EventData(Guid.NewGuid(), "event", false, new byte[3], null)).Wait();

				var dropped = new CountdownEvent(1);
				store.SubscribeToStreamFrom(stream, null,
					CatchUpSubscriptionSettings.Default,
					(x, y) => { throw new Exception("Error"); },
					_ => Log.Info("Live processing started."),
					(x, y, z) => dropped.Signal());
				Assert.IsTrue(dropped.Wait(Timeout));
			}
		}

		[Test, Category("LongRunning")]
		public void read_all_existing_events_and_keep_listening_to_new_ones() {
			const string stream = "read_all_existing_events_and_keep_listening_to_new_ones";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = new List<ResolvedEvent>();
				var appeared = new CountdownEvent(20); // events
				var dropped = new CountdownEvent(1);

				for (int i = 0; i < 10; ++i) {
					store.AppendToStreamAsync(stream, i - 1,
						new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null)).Wait();
				}

				var subscription = store.SubscribeToStreamFrom(stream,
					null,
					CatchUpSubscriptionSettings.Default,
					(x, y) => {
						events.Add(y);
						appeared.Signal();
						return Task.CompletedTask;
					},
					_ => Log.Info("Live processing started."),
					(x, y, z) => dropped.Signal());
				for (int i = 10; i < 20; ++i) {
					store.AppendToStreamAsync(stream, i - 1,
						new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null)).Wait();
				}

				if (!appeared.Wait(Timeout)) {
					Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
					Assert.Fail("Could not wait for all events.");
				}

				Assert.AreEqual(20, events.Count);
				for (int i = 0; i < 20; ++i) {
					Assert.AreEqual("et-" + i.ToString(), events[i].OriginalEvent.EventType);
				}

				Assert.IsFalse(dropped.Wait(0));
				subscription.Stop(Timeout);
				Assert.IsTrue(dropped.Wait(Timeout));
			}
		}

		[Test, Category("LongRunning")]
		public void filter_events_and_keep_listening_to_new_ones() {
			const string stream = "filter_events_and_keep_listening_to_new_ones";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = new List<ResolvedEvent>();
				var appeared = new CountdownEvent(20); // skip first 10 events
				var dropped = new CountdownEvent(1);

				for (int i = 0; i < 20; ++i) {
					store.AppendToStreamAsync(stream, i - 1,
						new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null)).Wait();
				}

				var subscription = store.SubscribeToStreamFrom(stream,
					9,
					CatchUpSubscriptionSettings.Default,
					(x, y) => {
						events.Add(y);
						appeared.Signal();
						return Task.CompletedTask;
					},
					_ => Log.Info("Live processing started."),
					(x, y, z) => dropped.Signal());
				for (int i = 20; i < 30; ++i) {
					store.AppendToStreamAsync(stream, i - 1,
						new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null)).Wait();
				}

				if (!appeared.Wait(Timeout)) {
					Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
					Assert.Fail("Could not wait for all events.");
				}

				Assert.AreEqual(20, events.Count);
				for (int i = 0; i < 20; ++i) {
					Assert.AreEqual("et-" + (i + 10).ToString(), events[i].OriginalEvent.EventType);
				}

				Assert.IsFalse(dropped.Wait(0));
				subscription.Stop(Timeout);
				Assert.IsTrue(dropped.Wait(Timeout));

				Assert.AreEqual(events.Last().OriginalEventNumber, subscription.LastProcessedEventNumber);

				subscription.Stop(TimeSpan.FromSeconds(0));
			}
		}

		[Test, Category("LongRunning")]
		public void filter_events_and_work_if_nothing_was_written_after_subscription() {
			const string stream = "filter_events_and_work_if_nothing_was_written_after_subscription";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var events = new List<ResolvedEvent>();
				var appeared = new CountdownEvent(10);
				var dropped = new CountdownEvent(1);

				for (int i = 0; i < 20; ++i) {
					store.AppendToStreamAsync(stream, i - 1,
						new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null)).Wait();
				}

				var subscription = store.SubscribeToStreamFrom(stream,
					9,
					CatchUpSubscriptionSettings.Default,
					(x, y) => {
						events.Add(y);
						appeared.Signal();
						return Task.CompletedTask;
					},
					_ => Log.Info("Live processing started."),
					(x, y, z) => dropped.Signal());
				if (!appeared.Wait(Timeout)) {
					Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
					Assert.Fail("Could not wait for all events.");
				}

				Assert.AreEqual(10, events.Count);
				for (int i = 0; i < 10; ++i) {
					Assert.AreEqual("et-" + (i + 10).ToString(), events[i].OriginalEvent.EventType);
				}

				Assert.IsFalse(dropped.Wait(0));
				subscription.Stop(Timeout);
				Assert.IsTrue(dropped.Wait(Timeout));

				Assert.AreEqual(events.Last().OriginalEventNumber, subscription.LastProcessedEventNumber);
			}
		}
	}
}
