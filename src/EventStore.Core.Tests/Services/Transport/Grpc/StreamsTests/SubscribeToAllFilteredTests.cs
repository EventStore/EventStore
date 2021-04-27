using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Tests.ClientAPI;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
using Position = EventStore.Core.Services.Transport.Grpc.Position;

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests {
	[TestFixture]
	public class SubscribeToAllFilteredTests {
		private static ClaimsPrincipal TestUser =>
			new ClaimsPrincipal(new ClaimsIdentity(new[] {new Claim(ClaimTypes.Name, "admin"),}, "ES-Test"));

		private static IEnumerable<EventData> CreateEvents(int count) => Enumerable.Range(0, count)
			.Select(i => new EventData(Guid.NewGuid(), i.ToString(), false, Array.Empty<byte>(), null));

		[TestFixtureSource(nameof(TestCases))]
		public class when_subscribing_to_all_with_a_filter<TLogFormat, TStreamId>
			: SpecificationWithMiniNode<TLogFormat, TStreamId> {
			private const string _streamName = "test";

			private int CheckpointCount => _positions.Count;

			private readonly uint _maxSearchWindow;
			private readonly int _filteredEventCount;
			private readonly uint _checkpointIntervalMultiplier;
			private readonly List<Position> _positions;
			private readonly TaskCompletionSource<bool> _complete;
			private readonly uint _checkpointInterval;

			private Position _position;
			private long _expected;

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

			public when_subscribing_to_all_with_a_filter(uint checkpointIntervalMultiplier, uint maxSearchWindow,
				int filteredEventCount) {
				_maxSearchWindow = maxSearchWindow;
				_checkpointIntervalMultiplier = checkpointIntervalMultiplier;
				_checkpointInterval = checkpointIntervalMultiplier * maxSearchWindow;
				_filteredEventCount = filteredEventCount;
				_positions = new List<Position>();
				_position = Position.End;
				_complete = new TaskCompletionSource<bool>();
			}

			protected override Task Given() =>
				_conn.AppendToStreamAsync("abcd", ExpectedVersion.NoStream, CreateEvents(_filteredEventCount));

			protected override async Task When() {
				var result = (await _conn.AppendToStreamAsync(_streamName, ExpectedVersion.NoStream, CreateEvents(1))
					.ConfigureAwait(false)).LogPosition;

				_position = new Position((ulong)result.CommitPosition, (ulong)result.PreparePosition);

				var skippedEventCount = (await _conn.ReadAllEventsForwardAsync(EventStore.ClientAPI.Position.Start,
						4096, false, new UserCredentials("admin", "changeit")).ConfigureAwait(false))
					.Events.Count(e => new Position((ulong)e.OriginalPosition.Value.CommitPosition,
						                   (ulong)e.OriginalPosition.Value.PreparePosition) <= _position &&
					                   e.OriginalEvent.EventStreamId != _streamName);
				_expected = skippedEventCount / _checkpointInterval;

				await using var enumerator = new Enumerators.AllSubscriptionFiltered(_node.Node.MainQueue,
					Position.Start, false, EventFilter.StreamName.Prefixes(true, _streamName), TestUser, false,
					_node.Node.ReadIndex, _maxSearchWindow, _checkpointIntervalMultiplier, p => {
						_positions.Add(p);

						if (p >= _position) {
							_complete.TrySetResult(true);
						}

						return Task.CompletedTask;
					}, CancellationToken.None);

				Assert.True(await enumerator.MoveNextAsync().ConfigureAwait(false));
				Assert.AreEqual(_streamName, enumerator.Current.OriginalStreamId);
			}

			[Test]
			public void receives_the_correct_number_of_checkpoints() {
				Assert.AreEqual(_expected, CheckpointCount);
			}

			[Test]
			public void no_duplicate_checkpoints_received() {
				Assert.AreEqual(_positions.Distinct().Count(), _positions.Count);
			}
		}
	}
}
