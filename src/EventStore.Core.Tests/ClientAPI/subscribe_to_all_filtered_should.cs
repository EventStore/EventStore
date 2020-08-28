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
using EventStore.Core.TransactionLog.Services;
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
		public override async Task SetUp() {
			await base.SetUp();
			_node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
			await _node.Start();

			_conn = BuildConnection(_node);
			await _conn.ConnectAsync();
			await _conn.SetStreamMetadataAsync("$all", -1,
				StreamMetadata.Build().SetReadRole(SystemRoles.All),
				new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword));

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
		public async Task only_return_events_with_a_given_stream_prefix() {
			var filter = Filter.StreamId.Prefix("stream-a");
			var foundEvents = new List<ResolvedEvent>();

			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				var appeared = new TaskCompletionSource<bool>();

				using (await store.FilteredSubscribeToAllAsync(false, filter, (s, e) => {
					foundEvents.Add(e);
					if (foundEvents.Count == 5) {
						appeared.TrySetResult(true);
					}
					return Task.CompletedTask;
				}, (s, p) => Task.CompletedTask, 100, (_, reason, ex) => appeared.TrySetException(ex))) {
					await _conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.EvenEvents());
					await _conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents.OddEvents());

					await appeared.Task.WithTimeout(Timeout);

					Assert.True(foundEvents.All(e => e.Event.EventStreamId == "stream-a"));
				}
			}
		}

		[Test, Category("LongRunning")]
		public async Task only_return_events_with_a_given_event_prefix() {
			var filter = Filter.EventType.Prefix("AE");
			var foundEvents = new List<ResolvedEvent>();

			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				var appeared = new TaskCompletionSource<bool>();

				using (await store.FilteredSubscribeToAllAsync(false, filter, (s, e) => {
					foundEvents.Add(e);
					if (foundEvents.Count == 5) {
						appeared.TrySetResult(true);
					}
					return Task.CompletedTask;
				}, (s, p) => Task.CompletedTask, 100)) {
					await _conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.EvenEvents());
					await _conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents.OddEvents());

					await appeared.Task.WithTimeout(Timeout);

					Assert.True(foundEvents.All(e => e.Event.EventType == "AEvent"));
				}
			}
		}

		[Test, Category("LongRunning")]
		public async Task only_return_events_that_satisfy_a_given_stream_regex() {
			var filter = Filter.StreamId.Regex(new Regex(@"^.*eam-b.*$"));
			var foundEvents = new List<ResolvedEvent>();

			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				var appeared = new TaskCompletionSource<bool>();

				using (await store.FilteredSubscribeToAllAsync(false, filter, (s, e) => {
					foundEvents.Add(e);
					if (foundEvents.Count == 5) {
						appeared.TrySetResult(true);
					}
					return Task.CompletedTask;
				}, (s, p) => Task.CompletedTask, 100)) {
					await _conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents.EvenEvents());
					await _conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.OddEvents());

					await appeared.Task.WithTimeout(Timeout);

					Assert.True(foundEvents.All(e => e.Event.EventStreamId == "stream-b"));
				}
			}
		}

		[Test, Category("LongRunning")]
		public async Task only_return_events_that_satisfy_a_given_event_regex() {
			var filter = Filter.EventType.Regex(new Regex(@"^.*BEv.*$"));
			var foundEvents = new ConcurrentBag<ResolvedEvent>();

			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				var appeared = new TaskCompletionSource<bool>();

				using (await store.FilteredSubscribeToAllAsync(false, filter, (s, e) => {
					foundEvents.Add(e);
					if (foundEvents.Count == 5) {
						appeared.TrySetResult(true);
					}
					return Task.CompletedTask;
				}, (s, p) => Task.CompletedTask, 100)) {
					await _conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents.EvenEvents());
					await _conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.OddEvents());

					await appeared.Task.WithTimeout(Timeout);

					Assert.True(foundEvents.All(e => e.Event.EventType == "BEvent"));
				}
			}
		}

		[Test, Category("LongRunning")]
		public async Task only_return_events_that_are_not_system_events() {
			var filter = Filter.ExcludeSystemEvents;
			var foundEvents = new List<ResolvedEvent>();

			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				var appeared = new TaskCompletionSource<bool>();

				using (await store.FilteredSubscribeToAllAsync(false, filter, (s, e) => {
					foundEvents.Add(e);
					if (foundEvents.Count == 5) {
						appeared.TrySetResult(true);
					}
					return Task.CompletedTask;
				}, (s, p) => Task.CompletedTask, 100)) {
					await _conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _fakeSystemEvents);
					await _conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.EvenEvents());

					await appeared.Task.WithTimeout(Timeout);

					Assert.True(foundEvents.All(e => !e.Event.EventType.StartsWith("$")));
				}
			}
		}

		[Test, Category("LongRunning")]
		public async Task throw_an_exception_if_interval_is_negative() {
			var filter = Filter.ExcludeSystemEvents;

			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();

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
		public async Task calls_checkpoint_reached_according_to_checkpoint_message_count() {
			var filter = Filter.ExcludeSystemEvents;

			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				var appeared = new TaskCompletionSource<bool>();
				var eventsSeen = 0;
				var checkpointsSeen = 0;

				using (await store.FilteredSubscribeToAllAsync(false,
					filter,
					(s, e) => {
						eventsSeen++;
						return Task.CompletedTask;
					},
					(s, p) => {
						if (++checkpointsSeen == 5) {
							appeared.TrySetResult(true);
						}
						return Task.CompletedTask;
					},
					2)) {
					await _conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents);

					await appeared.Task.WithTimeout(Timeout);

					Assert.AreEqual(10, eventsSeen);
				}
			}
		}

		[TearDown]
		public override async Task TearDown() {
			_conn.Close();
			await _node.Shutdown();
			await base.TearDown();
		}

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.Create(node.TcpEndPoint);
		}
	}
}
