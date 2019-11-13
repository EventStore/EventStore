using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), NonParallelizable]
	public class subscribe_to_all_filtered_should : SpecificationWithDirectory {
		private const int Timeout = 10000;

		private MiniNode _node;
		private IEventStoreConnection _conn;
		private List<EventData> _testEvents;
		private List<EventData> _fakeSystemEvents;

		[SetUp]
		public override void SetUp() {
			base.SetUp();
			_node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
			_node.Start();

			_conn = BuildConnection(_node);
			_conn.ConnectAsync().Wait();
			_conn.SetStreamMetadataAsync("$all", -1,
				StreamMetadata.Build().SetReadRole(SystemRoles.All),
				new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword)).Wait();

			_testEvents = Enumerable
				.Range(0, 5)
				.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "AEvent"))
				.ToList();

			_testEvents.AddRange(
				Enumerable
					.Range(0, 5)
					.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "BEvent")).ToList());

			_fakeSystemEvents = Enumerable
				.Range(0, 5)
				.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "$systemEvent"))
				.ToList();
		}

		[Test, Category("LongRunning")]
		public void only_return_events_with_a_given_stream_prefix() {
			var filter = Filter.StreamId.Prefix("stream-a");
			var foundEvents = new ConcurrentBag<ResolvedEvent>();

			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new CountdownEvent(4);

				using (store.FilteredSubscribeToAllAsync(false, filter, (s, e) => {
					foundEvents.Add(e);
					appeared.Signal();
					return Task.CompletedTask;
				}, (s, p) => Task.CompletedTask, 100).Result) {
					_conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.EvenEvents());
					_conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents.OddEvents());

					if (!appeared.Wait(Timeout)) {
						Assert.Fail("Appeared countdown event timed out.");
					}

					Assert.True(foundEvents.All(e => e.Event.EventStreamId == "stream-a"));
				}
			}
		}

		[Test, Category("LongRunning")]
		public void only_return_events_with_a_given_event_prefix() {
			var filter = Filter.EventType.Prefix("AE");
			var foundEvents = new ConcurrentBag<ResolvedEvent>();

			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new CountdownEvent(4);

				using (store.FilteredSubscribeToAllAsync(false, filter, (s, e) => {
					foundEvents.Add(e);
					appeared.Signal();
					return Task.CompletedTask;
				}, (s, p) => Task.CompletedTask, 100).Result) {
					_conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.EvenEvents());
					_conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents.OddEvents());

					if (!appeared.Wait(Timeout)) {
						Assert.Fail("Appeared countdown event timed out.");
					}

					Assert.True(foundEvents.All(e => e.Event.EventType == "AEvent"));
				}
			}
		}

		[Test, Category("LongRunning")]
		public void only_return_events_that_satisfy_a_given_stream_regex() {
			var filter = Filter.StreamId.Regex(new Regex(@"^.*eam-b.*$"));
			var foundEvents = new ConcurrentBag<ResolvedEvent>();

			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new CountdownEvent(4);

				using (store.FilteredSubscribeToAllAsync(false, filter, (s, e) => {
					foundEvents.Add(e);
					appeared.Signal();
					return Task.CompletedTask;
				}, (s, p) => Task.CompletedTask, 100).Result) {
					_conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents.EvenEvents());
					_conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.OddEvents());

					if (!appeared.Wait(Timeout)) {
						Assert.Fail("Appeared countdown event timed out.");
					}

					Assert.True(foundEvents.All(e => e.Event.EventStreamId == "stream-b"));
				}
			}
		}

		[Test, Category("LongRunning")]
		public void only_return_events_that_satisfy_a_given_event_regex() {
			var filter = Filter.EventType.Regex(new Regex(@"^.*BEv.*$"));
			var foundEvents = new ConcurrentBag<ResolvedEvent>();

			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new CountdownEvent(4);

				using (store.FilteredSubscribeToAllAsync(false, filter, (s, e) => {
					foundEvents.Add(e);
					appeared.Signal();
					return Task.CompletedTask;
				}, (s, p) => Task.CompletedTask, 100).Result) {
					_conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents.EvenEvents());
					_conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.OddEvents());

					if (!appeared.Wait(Timeout)) {
						Assert.Fail("Appeared countdown event timed out.");
					}

					Assert.True(foundEvents.All(e => e.Event.EventType == "BEvent"));
				}
			}
		}

		[Test, Category("LongRunning")]
		public void only_return_events_that_are_not_system_events() {
			var filter = Filter.ExcludeSystemEvents;
			var foundEvents = new ConcurrentBag<ResolvedEvent>();

			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new CountdownEvent(4);

				using (store.FilteredSubscribeToAllAsync(false, filter, (s, e) => {
					foundEvents.Add(e);
					appeared.Signal();
					return Task.CompletedTask;
				}, (s, p) => Task.CompletedTask, 100).Result) {
					_conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _fakeSystemEvents);
					_conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.EvenEvents());

					if (!appeared.Wait(Timeout)) {
						Assert.Fail("Appeared countdown event timed out.");
					}

					Assert.True(foundEvents.All(e => !e.Event.EventType.StartsWith("$")));
				}
			}
		}

		[Test, Category("LongRunning")]
		public void throw_an_exception_if_interval_is_negative() {
			var filter = Filter.ExcludeSystemEvents;

			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				Assert.Throws<ArgumentOutOfRangeException>(() => {
					store.FilteredSubscribeToAllAsync(
						false,
						filter,
						(s, e) => Task.CompletedTask,
						(s, p) => Task.CompletedTask, 0).Wait();
				});
			}
		}

		[Test, Category("LongRunning")]
		public void calls_checkpoint_reached_according_to_checkpoint_message_count() {
			var filter = Filter.ExcludeSystemEvents;

			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var appeared = new CountdownEvent(5);
				var eventsSeen = 0;

				using (store.FilteredSubscribeToAllAsync(false,
					filter,
					(s, e) => {
						eventsSeen++;
						return Task.CompletedTask;
					},
					(s, p) => {
						appeared.Signal();
						return Task.CompletedTask;
					},
					2).Result) {
					_conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents);

					if (!appeared.Wait(Timeout)) {
						Assert.Fail("Checkpoint appeared not called enough times within time limit.");
					}

					Assert.AreEqual(10, eventsSeen);
				}
			}
		}

		[TearDown]
		public override void TearDown() {
			_conn.Close();
			_node.Shutdown();
			base.TearDown();
		}

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.Create(node.TcpEndPoint);
		}
	}
}
