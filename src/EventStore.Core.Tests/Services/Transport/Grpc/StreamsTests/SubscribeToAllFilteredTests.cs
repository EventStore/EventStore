using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Tests.ClientAPI;
using NUnit.Framework;
using Position = EventStore.Core.Services.Transport.Grpc.Position;

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests {
	[TestFixture]
	public class SubscribeToAllFilteredTests {
		public static IEnumerable<object[]> TestCases() {
			var checkpointIntervalMultipliers = new uint[] {2, 4, 8};

			var maxSearchWindows = new uint[] {1, 32, 64};

			var filteredEventCount = checkpointIntervalMultipliers.Max() * maxSearchWindows.Max();
			return from checkpointInterval in checkpointIntervalMultipliers
				from maxSearchWindow in maxSearchWindows
				select new object[] {
					checkpointInterval,
					maxSearchWindow,
					(int)filteredEventCount
				};
		}

		private static ClaimsPrincipal TestUser =>
			new ClaimsPrincipal(new ClaimsIdentity(new[] {new Claim(ClaimTypes.Name, "admin"),}, "ES-Test"));

		private static IEnumerable<EventData> CreateEvents(int count) => Enumerable.Range(0, count)
			.Select(i => new EventData(Guid.NewGuid(), i.ToString(), false, Array.Empty<byte>(), null));

		public class when_subscribing_to_all_with_a_filter : SpecificationWithMiniNode {
			private const string _streamName = "test";
			private const int EventCount = 10;
			private readonly TaskCompletionSource<int> _checkpointsSeen = new TaskCompletionSource<int>();
			private readonly TaskCompletionSource<Position> _firstCheckpoint = new TaskCompletionSource<Position>();

			protected override async Task Given() {
				await _conn.AppendToStreamAsync("abcd", ExpectedVersion.NoStream, CreateEvents(EventCount));
				await _conn.AppendToStreamAsync(_streamName, ExpectedVersion.NoStream, CreateEvents(1));
			}

			protected override async Task When() {
				var checkpointCount = 0;
				await using var enumerator = new Enumerators.AllSubscriptionFiltered(_node.Node.MainQueue,
					Position.Start, false, EventFilter.StreamName.Prefixes(_streamName), TestUser, false,
					_node.Node.ReadIndex, 1, 2, position => {
						_firstCheckpoint.TrySetResult(position);
						checkpointCount++;
						if (checkpointCount > 5) {
							_checkpointsSeen.TrySetResult(checkpointCount);
						}
						return Task.CompletedTask;
					}, CancellationToken.None);

				Assert.IsTrue(await enumerator.MoveNextAsync().ConfigureAwait(false));
				Assert.AreEqual(_streamName, enumerator.Current.Event.EventStreamId);
			}

			[Test]
			public async Task receives_the_correct_number_of_checkpoints() {
				var checkpointCount = await _checkpointsSeen.Task.WithTimeout();
				Assert.AreEqual(6, checkpointCount);
			}
		}

		[TestFixtureSource(typeof(SubscribeToAllFilteredTests), nameof(TestCases))]
		public class when_subscribing_to_all_with_a_filter_live : SpecificationWithMiniNode {
			private const string StreamName = "test";

			private int CheckpointCount => _positions.Count;

			private readonly uint _maxSearchWindow;
			private readonly int _filteredEventCount;
			private readonly uint _checkpointIntervalMultiplier;
			private readonly List<Position> _positions;

			private Position _position;

			public when_subscribing_to_all_with_a_filter_live(uint checkpointIntervalMultiplier, uint maxSearchWindow,
				int filteredEventCount) {
				_maxSearchWindow = maxSearchWindow;
				_checkpointIntervalMultiplier = checkpointIntervalMultiplier;
				_filteredEventCount = filteredEventCount;
				_positions = new List<Position>();
				_position = Position.End;
			}

			protected override async Task Given() {
				await _conn.AppendToStreamAsync("abcd", ExpectedVersion.Any, CreateEvents(_filteredEventCount));
			}

			protected override async Task When() {
				var filter = EventFilter.StreamName.Prefixes(StreamName);
				await using var enumerator = new Enumerators.AllSubscriptionFiltered(_node.Node.MainQueue, Position.End,
					false,
					filter, TestUser, true, _node.Node.ReadIndex, _maxSearchWindow, _checkpointIntervalMultiplier,
					CheckpointReached, default);

				var success = await _conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any, CreateEvents(1));
				_position = new Position((ulong)success.LogPosition.CommitPosition, (ulong)success.LogPosition.PreparePosition);

				while (await enumerator.MoveNextAsync()) {
					var response = enumerator.Current;
					Assert.AreEqual(StreamName, response.Event.EventStreamId);
					return;
				}

				Task CheckpointReached(Position arg) {
					_positions.Add(arg);
					return Task.CompletedTask;
				}
			}

			[Test]
			public void receives_the_correct_number_of_checkpoints() {
				Assert.AreEqual(1, CheckpointCount);
			}

			[Test]
			public void no_duplicate_checkpoints_received() {
				Assert.AreEqual(_positions.Distinct().Count(), _positions.Count);
			}

			[Test]
			public void checkpoint_is_before_last_written_event() {
				Assert.True(_positions[0] <= _position);
			}
		}
	}
}
