using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Tests.Streams {
	[Trait("Category", "LongRunning")]
	public class stream_subscription : IClassFixture<stream_subscription.Fixture> {
		private readonly Fixture _fixture;

		public stream_subscription(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task subscribe_to_non_existing_stream_and_then_catch_new_event() {
			var stream = _fixture.GetStreamName();
			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<bool>();

			using var _ = _fixture.Client.SubscribeToStream(stream, StreamRevision.End, (s, e, ct) => {
				appeared.TrySetResult(true);
				return Task.CompletedTask;
			}, false, (s, reason, ex) => dropped.TrySetResult(true));
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents());

			Assert.True(await appeared.Task.WithTimeout());
		}

		[Fact]
		public async Task allow_multiple_subscriptions_to_same_stream() {
			var stream = _fixture.GetStreamName();

			var appeared = new TaskCompletionSource<bool>();

			int appearedCount = 0;

			using var s1 = _fixture.Client.SubscribeToStream(stream, StreamRevision.End, EventAppeared, false);
			using var s2 = _fixture.Client.SubscribeToStream(stream, StreamRevision.End, EventAppeared, false);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents());

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

			using var _ = _fixture.Client.SubscribeToStream(stream, StreamRevision.End, EventAppeared, false, SubscriptionDropped);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents());

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				s.Dispose();
				return Task.CompletedTask;
			}

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex)
				=> dropped.SetResult((reason, ex));

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);
		}

		[Fact]
		public async Task catches_deletions() {
			var stream = _fixture.GetStreamName();

			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			using var _ = _fixture.Client.SubscribeToStream(stream, StreamRevision.End, EventAppeared, false, SubscriptionDropped);

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
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
