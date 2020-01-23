using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class subscribe_to_stream : IClassFixture<subscribe_to_stream.Fixture> {
		private readonly Fixture _fixture;

		public subscribe_to_stream(Fixture fixture, ITestOutputHelper outputHelper) {
			_fixture = fixture;
			_fixture.CaptureLogs(outputHelper);
		}

		[Fact]
		public async Task subscribe_to_non_existing_stream() {
			var stream = _fixture.GetStreamName();
			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			using var subscription =
				await _fixture.Client.SubscribeToStreamAsync(stream, EventAppeared, false, SubscriptionDropped);

			Assert.False(appeared.Task.IsCompleted);

			if (dropped.Task.IsCompleted) {
				Assert.False(dropped.Task.IsCompleted, dropped.Task.Result.ToString());
			}

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();
			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				appeared.TrySetResult(true);
				return Task.CompletedTask;
			}

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex)
				=> dropped.SetResult((reason, ex));
		}

		[Fact]
		public async Task subscribe_to_non_existing_stream_then_get_event() {
			var stream = _fixture.GetStreamName();
			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			using var subscription =
				await _fixture.Client.SubscribeToStreamAsync(stream, EventAppeared, false, SubscriptionDropped);

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents());

			Assert.True(await appeared.Task.WithTimeout());

			if (dropped.Task.IsCompleted) {
				Assert.False(dropped.Task.IsCompleted, dropped.Task.Result.ToString());
			}

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();
			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				appeared.TrySetResult(true);
				return Task.CompletedTask;
			}

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex)
				=> dropped.SetResult((reason, ex));
		}

		[Fact]
		public async Task allow_multiple_subscriptions_to_same_stream() {
			var stream = _fixture.GetStreamName();

			var appeared = new TaskCompletionSource<bool>();

			int appearedCount = 0;

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents());
			using var s1 = await _fixture.Client.SubscribeToStreamAsync(stream, EventAppeared).WithTimeout();
			using var s2 = await _fixture.Client.SubscribeToStreamAsync(stream, EventAppeared).WithTimeout();

			Assert.True(await appeared.Task.WithTimeout());

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				if (++appearedCount == 2) {
					appeared.TrySetResult(true);
				}

				return Task.CompletedTask;
			}
		}

		[Fact]
		public async Task calls_subscription_dropped_when_disposed() {
			var stream = _fixture.GetStreamName();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			using var subscription =
				await _fixture.Client.SubscribeToStreamAsync(stream, EventAppeared, false, SubscriptionDropped);

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

			using var subscription =
				await _fixture.Client.SubscribeToStreamAsync(stream, EventAppeared, false, SubscriptionDropped)
					.WithTimeout();

			if (dropped.Task.IsCompleted) {
				Assert.False(dropped.Task.IsCompleted, dropped.Task.Result.ToString());
			}

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents());

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.SubscriberError, reason);
			Assert.Same(expectedException, ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) =>
				Task.FromException(expectedException);

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex) =>
				dropped.SetResult((reason, ex));
		}

		[Fact]
		public async Task reads_all_existing_events_and_keep_listening_to_new_ones() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(20).ToArray();

			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			var beforeEvents = events.Take(10);
			var afterEvents = events.Skip(10);

			using var enumerator = events.AsEnumerable().GetEnumerator();

			enumerator.MoveNext();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, beforeEvents)
				.WithTimeout();

			using var subscription =
				await _fixture.Client.SubscribeToStreamAsync(stream, EventAppeared, false, SubscriptionDropped)
					.WithTimeout();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, afterEvents)
				.WithTimeout();

			await appeared.Task.WithTimeout();

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
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
		}

		[Fact]
		public async Task catches_deletions() {
			var stream = _fixture.GetStreamName();

			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			using var _ = await _fixture.Client.SubscribeToStreamAsync(stream, EventAppeared, false, SubscriptionDropped);

			await _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.NoStream);
			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.ServerError, reason);
			Assert.IsType<StreamDeletedException>(ex);
			Assert.Equal(stream, ((StreamDeletedException)ex).Stream);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) => Task.CompletedTask;

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex) =>
				dropped.SetResult((reason, ex));
		}


		public class Fixture : EventStoreGrpcFixture {
			public EventData[] Events { get; }

			public Fixture() {
				Events = CreateTestEvents(10).ToArray();
			}

			protected override Task Given() => Task.CompletedTask;

			protected override Task When() => Task.CompletedTask;
		}
	}
}
