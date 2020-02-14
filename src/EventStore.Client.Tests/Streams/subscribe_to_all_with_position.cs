using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class subscribe_to_all_with_position : IAsyncLifetime, IDisposable {
		private readonly Fixture _fixture;
		private readonly IDisposable _loggingContext;

		/// <summary>
		/// This class does not implement IClassFixture because it checks $all, and we want a fresh Node for each test.
		/// </summary>
		public subscribe_to_all_with_position(ITestOutputHelper outputHelper) {
			_fixture = new Fixture();
			_loggingContext = LoggingHelper.Capture(outputHelper);
		}

		[Fact]
		public async Task calls_subscription_dropped_when_disposed() {
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			var firstEvent = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, 1)
				.FirstOrDefaultAsync();

			using var subscription = await _fixture.Client.SubscribeToAllAsync(firstEvent.OriginalEvent.Position, EventAppeared,
				false, SubscriptionDropped);

			if (dropped.Task.IsCompleted) {
				Assert.False(dropped.Task.IsCompleted, dropped.Task.Result.ToString());
			}

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) => Task.CompletedTask;

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex) =>
				dropped.SetResult((reason, ex));
		}

		[Fact]
		public async Task calls_subscription_dropped_when_error_processing_event() {
			var stream = _fixture.GetStreamName();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();
			var expectedException = new Exception("Error");

			var firstEvent = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, 1)
				.FirstOrDefaultAsync();

			using var subscription = _fixture.Client.SubscribeToAllAsync(firstEvent.OriginalEvent.Position, EventAppeared,
				false, SubscriptionDropped);

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents(2));

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.SubscriberError, reason);
			Assert.Same(expectedException, ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) =>
				Task.FromException(expectedException);

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex) =>
				dropped.SetResult((reason, ex));
		}

		[Fact]
		public async Task subscribe_to_empty_database() {
			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			var firstEvent = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, 1)
				.FirstOrDefaultAsync();

			using var subscription = await _fixture.Client.SubscribeToAllAsync(firstEvent.OriginalEvent.Position, EventAppeared,
				false, SubscriptionDropped);

			Assert.False(appeared.Task.IsCompleted);

			if (dropped.Task.IsCompleted) {
				Assert.False(dropped.Task.IsCompleted, dropped.Task.Result.ToString());
			}

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				if (e.OriginalEvent.Position == firstEvent.OriginalEvent.Position) {
					appeared.TrySetException(new Exception());
					return Task.CompletedTask;
				}

				if (!Core.Services.SystemStreams.IsSystemStream(e.OriginalStreamId)) {
					appeared.TrySetResult(true);
				}

				return Task.CompletedTask;
			}

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex) =>
				dropped.SetResult((reason, ex));
		}

		[Fact]
		public async Task reads_all_existing_events_after_position_and_keep_listening_to_new_ones() {
			var events = _fixture.CreateTestEvents(20).ToArray();

			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			var beforeEvents = events.Take(10);
			var afterEvents = events.Skip(10);

			using var enumerator = events.AsEnumerable().GetEnumerator();

			enumerator.MoveNext();

			var position = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, 1)
				.Select(x => x.OriginalEvent.Position)
				.FirstAsync();

			foreach (var @event in beforeEvents) {
				await _fixture.Client.AppendToStreamAsync($"stream-{@event.EventId:n}", AnyStreamRevision.NoStream,
					new[] {@event});
			}

			using var subscription =
				await _fixture.Client.SubscribeToAllAsync(position, EventAppeared, false, SubscriptionDropped);

			foreach (var @event in afterEvents) {
				await _fixture.Client.AppendToStreamAsync($"stream-{@event.EventId:n}", AnyStreamRevision.NoStream,
					new[] {@event});
			}

			await appeared.Task.WithTimeout();

			Assert.False(dropped.Task.IsCompleted);

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				if (position >= e.OriginalEvent.Position) {
					appeared.TrySetException(new Exception());
				}

				if (!SystemStreams.IsSystemStream(e.OriginalStreamId)) {
					try {
						Assert.Equal(enumerator.Current.EventId, e.OriginalEvent.EventId);
						if (!enumerator.MoveNext()) {
							appeared.TrySetResult(true);
						}
					} catch (Exception ex) {
						appeared.TrySetException(ex);
						throw;
					}
				}

				return Task.CompletedTask;
			}

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex) =>
				dropped.SetResult((reason, ex));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() =>
				Client.SetStreamMetadataAsync("$all", AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(SystemRoles.All)), userCredentials: TestCredentials.Root);

			protected override Task When() => Task.CompletedTask;
		}

		public Task InitializeAsync() => _fixture.InitializeAsync();
		public Task DisposeAsync() => _fixture.DisposeAsync();
		public void Dispose() => _loggingContext.Dispose();
	}
}
