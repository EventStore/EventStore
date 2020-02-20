using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class subscribe_to_stream_with_revision : IAsyncLifetime {
		private readonly Fixture _fixture;
		public subscribe_to_stream_with_revision(ITestOutputHelper outputHelper) {
			_fixture = new Fixture();
			_fixture.CaptureLogs(outputHelper);
		}

		[Fact]
		public async Task subscribe_to_non_existing_stream() {
			var stream = _fixture.GetStreamName();
			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			using var subscription = await _fixture.Client.SubscribeToStreamAsync(stream, StreamRevision.Start, EventAppeared,
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

			using var subscription = await _fixture.Client.SubscribeToStreamAsync(stream, StreamRevision.Start, EventAppeared,
				false, SubscriptionDropped);

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents(2));

			Assert.True(await appeared.Task.WithTimeout());

			if (dropped.Task.IsCompleted) {
				Assert.False(dropped.Task.IsCompleted, dropped.Task.Result.ToString());
			}

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();
			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				if (e.OriginalEvent.EventNumber == StreamRevision.Start) {
					appeared.TrySetException(new Exception());
				} else {
					appeared.TrySetResult(true);
				}

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

			using var s1 = await _fixture.Client.SubscribeToStreamAsync(stream, StreamRevision.Start, EventAppeared);
			using var s2 = await _fixture.Client.SubscribeToStreamAsync(stream, StreamRevision.Start, EventAppeared);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents(2));

			Assert.True(await appeared.Task.WithTimeout());

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				if (e.OriginalEvent.EventNumber == StreamRevision.Start) {
					appeared.TrySetException(new Exception());
					return Task.CompletedTask;
				}

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

			using var subscription = await _fixture.Client.SubscribeToStreamAsync(stream, StreamRevision.Start, EventAppeared,
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

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents(2));

			using var subscription = await _fixture.Client.SubscribeToStreamAsync(stream, StreamRevision.Start, EventAppeared,
				false, SubscriptionDropped);

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

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents());

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, beforeEvents);

			using var subscription =
				await _fixture.Client.SubscribeToStreamAsync(stream, StreamRevision.Start, EventAppeared, false,
					SubscriptionDropped);

			var writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, afterEvents);

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

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex) {
				dropped.SetResult((reason, ex));
			}
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}

		public Task InitializeAsync() => _fixture.InitializeAsync();

		public Task DisposeAsync() => _fixture.DisposeAsync();
	}
}
