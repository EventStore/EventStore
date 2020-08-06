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
	}
}
