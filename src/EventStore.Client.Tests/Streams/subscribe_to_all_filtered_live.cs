using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.Client.Streams {
	public class subscribe_to_all_filtered_live : IAsyncLifetime {
		private readonly Fixture _fixture;
		public subscribe_to_all_filtered_live(ITestOutputHelper outputHelper) {
			_fixture = new Fixture();
			_fixture.CaptureLogs(outputHelper);
		}

		public static IEnumerable<object[]> FilterCases() => Filters.All.Select(filter => new object[] {filter});

		[Theory, MemberData(nameof(FilterCases))]
		public async Task does_not_read_all_events_but_keep_listening_to_new_ones(string filterName) {
			var streamPrefix = _fixture.GetStreamName();
			var (getFilter, prepareEvent) = Filters.GetFilter(filterName);

			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();
			var checkpointSeen = new TaskCompletionSource<bool>();
			var filter = getFilter(streamPrefix);
			var events = _fixture.CreateTestEvents(20).Select(e => prepareEvent(streamPrefix, e))
				.ToArray();
			var beforeEvents = events.Take(10);
			var afterEvents = events.Skip(10);

			using var enumerator = afterEvents.OfType<EventData>().GetEnumerator();
			enumerator.MoveNext();

			foreach (var e in beforeEvents) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			using var subscription = await _fixture.Client.SubscribeToAllAsync(Position.End, EventAppeared, false,
				filterOptions: new FilterOptions(filter, 5, CheckpointReached),
				subscriptionDropped: SubscriptionDropped);

			foreach (var e in afterEvents) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			await Task.WhenAll(appeared.Task, checkpointSeen.Task).WithTimeout();

			Assert.False(dropped.Task.IsCompleted);

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription _, ResolvedEvent e, CancellationToken ct) {
				try {
					Assert.Equal(enumerator.Current.EventId, e.OriginalEvent.EventId);
					if (!enumerator.MoveNext()) {
						appeared.TrySetResult(true);
					}
				} catch (Exception ex) {
					appeared.TrySetException(ex);
					throw;
				}

				return Task.CompletedTask;
			}

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex) =>
				dropped.SetResult((reason, ex));

			Task CheckpointReached(StreamSubscription _, Position position, CancellationToken ct) {
				checkpointSeen.TrySetResult(true);

				return Task.CompletedTask;
			}
		}

		public Task InitializeAsync() => _fixture.InitializeAsync();

		public Task DisposeAsync() => _fixture.DisposeAsync();

		public class Fixture : EventStoreGrpcFixture {
			public const string FilteredOutStream = nameof(FilteredOutStream);

			protected override Task Given() => Client.SetStreamMetadataAsync("$all", AnyStreamRevision.Any,
				new StreamMetadata(acl: new StreamAcl(SystemRoles.All)), userCredentials: TestCredentials.Root);

			protected override Task When() =>
				Client.AppendToStreamAsync(FilteredOutStream, AnyStreamRevision.NoStream, CreateTestEvents(10));
		}
	}
}
