extern alias GrpcClient;
extern alias GrpcClientStreams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using GrpcClientStreams::EventStore.Client;
using NUnit.Framework;
using EventData = GrpcClient::EventStore.Client.EventData;
using ILogger = Serilog.ILogger;
using Position = GrpcClient::EventStore.Client.Position;
using ResolvedEvent = GrpcClient::EventStore.Client.ResolvedEvent;
using Uuid = GrpcClient::EventStore.Client.Uuid;

namespace EventStore.Core.Tests.ClientAPI {
	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_to_all_catching_up_should<TLogFormat, TStreamId> : SpecificationWithDirectory {
		private static readonly ILogger Log = Serilog.Log.ForContext<subscribe_to_all_catching_up_should<TLogFormat, TStreamId>>();
		private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

		private MiniNode<TLogFormat, TStreamId> _node;
		private IEventStoreClient _conn;

		[SetUp]
		public override async Task SetUp() {
			await base.SetUp();
			_node = new MiniNode<TLogFormat, TStreamId>(PathName);
			await _node.Start();

			_conn = BuildConnection(_node);
			await _conn.ConnectAsync();
			await _conn.SetStreamMetadataAsync("$all", -1,
				new StreamMetadata(acl: new StreamAcl(readRole: SystemRoles.All)),
				DefaultData.AdminCredentials);
		}

		[TearDown]
		public override async Task TearDown() {
			await _conn.Close();
			await _node.Shutdown();
			await base.TearDown();
		}

		protected virtual IEventStoreClient BuildConnection(MiniNode<TLogFormat, TStreamId> node) {
			return new GrpcEventStoreConnection(node.HttpEndPoint);
		}

		[Test, Category("LongRunning")]
		public async Task call_dropped_callback_after_stop_method_call() {
			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();

				var dropped = new CountdownEvent(1);
				var subscription = store.SubscribeToAllFrom(null,
					CatchUpSubscriptionSettings.Default,
					(x, y) => Task.CompletedTask,
					_ => Log.Information("Live processing started."),
					(x, y, z) => dropped.Signal());

				Assert.IsFalse(dropped.Wait(0));
				subscription.Dispose();
				Assert.IsTrue(dropped.Wait(Timeout));
			}
		}

		[Test, Category("LongRunning")]
		public async Task call_dropped_callback_when_an_error_occurs_while_processing_an_event() {
			const string stream = "call_dropped_callback_when_an_error_occurs_while_processing_an_event";
			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				await store.AppendToStreamAsync(stream, ExpectedVersion.Any,
					new EventData(Uuid.NewUuid(), "event", new byte[3], null));

				//var dropped = new CountdownEvent(1);
				var dropped = new TaskCompletionSource<bool>();
				await store.SubscribeToAllFrom(null, CatchUpSubscriptionSettings.Default,
					(x, y) => { throw new Exception("Error"); },
					_ => Log.Information("Live processing started."),
					(x, y, z) => dropped.TrySetResult(true));
				var source = new CancellationTokenSource(Timeout);
				Assert.IsTrue(await dropped.Task.WaitAsync(source.Token));
			}
		}

		[Test, Category("LongRunning")]
		public async Task be_able_to_subscribe_to_empty_db() {
			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				var appeared = new ManualResetEventSlim(false);
				var dropped = new CountdownEvent(1);

				var subscription = await store.SubscribeToAllFrom(null,
					CatchUpSubscriptionSettings.Default,
					(_, x) => {
						if (!SystemStreams.IsSystemStream(x.OriginalEvent.EventStreamId))
							appeared.Set();
						return Task.CompletedTask;
					},
					_ => Log.Information("Live processing started."),
					(_, __, ___) => dropped.Signal());

				await Task.Delay(100); // give time for first pull phase
				await store.SubscribeToAllAsync(false, (s, x) => Task.CompletedTask, (s, r, e) => { });
				await Task.Delay(100);

				Assert.IsFalse(appeared.Wait(0), "Some event appeared.");
				Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
				subscription.Dispose();
				Assert.IsTrue(dropped.Wait(Timeout));
			}
		}

		[Test, Category("LongRunning")]
		public async Task read_all_existing_events_and_keep_listening_to_new_ones() {
			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();

				var events = new List<ResolvedEvent>();
				var appeared = new CountdownEvent(20);
				var dropped = new CountdownEvent(1);

				for (int i = 0; i < 10; ++i) {
					await store.AppendToStreamAsync("stream-" + i.ToString(), -1,
						new EventData(Uuid.NewUuid(), "et-" + i.ToString(), new byte[3], null));
				}

				var subscription = await store.SubscribeToAllFrom(null,
					CatchUpSubscriptionSettings.Default,
					(x, y) => {
						if (!SystemStreams.IsSystemStream(y.OriginalEvent.EventStreamId)) {
							events.Add(y);
							appeared.Signal();
						}

						return Task.CompletedTask;
					},
					_ => Log.Information("Live processing started."),
					(x, y, z) => dropped.Signal());
				for (int i = 10; i < 20; ++i) {
					await store.AppendToStreamAsync("stream-" + i.ToString(), -1,
						new EventData(Uuid.NewUuid(), "et-" + i.ToString(), new byte[3], null));
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
				subscription.Dispose();
				Assert.IsTrue(dropped.Wait(Timeout));
			}
		}

		[Test, Category("LongRunning")]
		public async Task filter_events_and_keep_listening_to_new_ones() {
			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();

				var events = new System.Collections.Generic.List<ResolvedEvent>();
				var appeared = new CountdownEvent(10);
				var dropped = new CountdownEvent(1);

				for (int i = 0; i < 10; ++i) {
					await store.AppendToStreamAsync("stream-" + i.ToString(), -1,
						new EventData(Uuid.NewUuid(), "et-" + i.ToString(), new byte[3], null));
				}

				var allSlice = await store.ReadAllEventsForwardAsync(Position.Start, 100, false);
				var lastEvent = allSlice.Events.Last();

				var lastProcessedPosition = -1L;
				var subscription = store.SubscribeToAllFrom(lastEvent.OriginalPosition,
					CatchUpSubscriptionSettings.Default,
					(x, y) => {
						if (SystemStreams.IsSystemStream(y.Event.EventStreamId)) {
							return Task.CompletedTask;
						}

						Interlocked.Exchange(ref lastProcessedPosition, (long)y.OriginalPosition!.Value.CommitPosition);
						events.Add(y);
						appeared.Signal();
						return Task.CompletedTask;
					},
					_ => Log.Information("Live processing started."),
					(x, y, z) => {
						Log.Information("Subscription dropped: {0}, {1}.", y, z);
						dropped.Signal();
					});

				for (int i = 10; i < 20; ++i) {
					await store.AppendToStreamAsync("stream-" + i.ToString(), -1,
						new EventData(Uuid.NewUuid(), "et-" + i.ToString(), new byte[3], null));
				}

				Log.Information("Waiting for events...");
				if (!appeared.Wait(Timeout)) {
					Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
					Assert.Fail("Could not wait for all events.");
				}

				Log.Information("Events appeared...");
				Assert.AreEqual(10, events.Count);
				for (int i = 0; i < 10; ++i) {
					Assert.AreEqual("et-" + (10 + i).ToString(), events[i].OriginalEvent.EventType);
				}

				Assert.IsFalse(dropped.Wait(0));
				subscription.Dispose();
				Assert.IsTrue(dropped.Wait(Timeout));

				Assert.AreEqual((long)events.Last().OriginalPosition!.Value.CommitPosition, lastProcessedPosition);
			}
		}

		[Test, Category("LongRunning")]
		public async Task filter_events_and_work_if_nothing_was_written_after_subscription() {
			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();

				var events = new System.Collections.Generic.List<ResolvedEvent>();
				var appeared = new CountdownEvent(1);
				var dropped = new CountdownEvent(1);

				for (int i = 0; i < 10; ++i) {
					await store.AppendToStreamAsync("stream-" + i.ToString(), -1,
						new EventData(Uuid.NewUuid(), "et-" + i.ToString(), new byte[3], null));
				}

				var allSlice = await store.ReadAllEventsForwardAsync(Position.Start, 100, false);
				var lastEvent = allSlice.Events[allSlice.Events.Length - 2];

				var lastProcessedPosition = -1L;
				var subscription = store.SubscribeToAllFrom(lastEvent.OriginalPosition,
					CatchUpSubscriptionSettings.Default,
					(x, y) => {
						Interlocked.Exchange(ref lastProcessedPosition, (long)y.OriginalPosition!.Value.CommitPosition);
						events.Add(y);
						appeared.Signal();
						return Task.CompletedTask;
					},
					_ => Log.Information("Live processing started."),
					(x, y, z) => {
						Log.Information("Subscription dropped: {0}, {1}.", y, z);
						dropped.Signal();
					});

				Log.Information("Waiting for events...");
				if (!appeared.Wait(Timeout)) {
					Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
					Assert.Fail("Could not wait for all events.");
				}

				Log.Information("Events appeared...");
				Assert.AreEqual(1, events.Count);
				Assert.AreEqual("et-9", events[0].OriginalEvent.EventType);

				Assert.IsFalse(dropped.Wait(0));
				subscription.Dispose();
				Assert.IsTrue(dropped.Wait(Timeout));

				Assert.AreEqual((long)events.Last().OriginalPosition!.Value.CommitPosition, lastProcessedPosition);
			}
		}
	}
}
