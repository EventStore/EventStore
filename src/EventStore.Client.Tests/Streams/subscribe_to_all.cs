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
	public class subscribe_to_all : IAsyncLifetime, IDisposable {
		private readonly Fixture _fixture;
		private readonly IDisposable _loggingContext;

		/// <summary>
		/// This class does not implement IClassFixture because it checks $all, and we want a fresh Node for each test.
		/// </summary>
		public subscribe_to_all(ITestOutputHelper outputHelper) {
			_fixture = new Fixture();
			_loggingContext = LoggingHelper.Capture(outputHelper);
		}

		[Fact]
		public async Task calls_subscription_dropped_when_disposed() {
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			using var subscription =
				await _fixture.Client.SubscribeToAllAsync(EventAppeared, false, SubscriptionDropped);

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
				await _fixture.Client.SubscribeToAllAsync(EventAppeared, false, SubscriptionDropped).WithTimeout();

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
		public async Task subscribe_to_empty_database() {
			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			using var subscription = await _fixture.Client.SubscribeToAllAsync(EventAppeared, false, SubscriptionDropped);

			Assert.False(appeared.Task.IsCompleted);

			if (dropped.Task.IsCompleted) {
				Assert.False(dropped.Task.IsCompleted, dropped.Task.Result.ToString());
			}

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				if (!Core.Services.SystemStreams.IsSystemStream(e.OriginalStreamId)) {
					appeared.TrySetResult(true);
				}

				return Task.CompletedTask;
			}

			void SubscriptionDropped(StreamSubscription s, SubscriptionDroppedReason reason, Exception ex) =>
				dropped.SetResult((reason, ex));
		}

		[Fact]
		public async Task reads_all_existing_events_and_keep_listening_to_new_ones() {
			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();
			var appearedEvents = new List<EventRecord>();
			var beforeEvents = _fixture.CreateTestEvents(10).ToArray();
			var afterEvents = _fixture.CreateTestEvents(10).ToArray();

			foreach (var @event in beforeEvents) {
				await _fixture.Client.AppendToStreamAsync($"stream-{@event.EventId:n}", AnyStreamRevision.NoStream,
					new[] {@event});
			}

			using var subscription = await _fixture.Client.SubscribeToAllAsync(EventAppeared, false, SubscriptionDropped);

			foreach (var @event in afterEvents) {
				await _fixture.Client.AppendToStreamAsync($"stream-{@event.EventId:n}", AnyStreamRevision.NoStream,
					new[] {@event});
			}

			await appeared.Task.WithTimeout();

			Assert.Equal(beforeEvents.Concat(afterEvents).Select(x => x.EventId),
				appearedEvents.Select(x => x.EventId));

			if (dropped.Task.IsCompleted) {
				Assert.False(dropped.Task.IsCompleted, dropped.Task.Result.ToString());
			}

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				if (!Core.Services.SystemStreams.IsSystemStream(e.OriginalStreamId)) {
					appearedEvents.Add(e.Event);

					if (appearedEvents.Count >= beforeEvents.Length + afterEvents.Length) {
						appeared.TrySetResult(true);
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
