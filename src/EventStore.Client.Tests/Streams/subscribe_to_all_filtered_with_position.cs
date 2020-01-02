using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.Client.Streams {
	public class subscribe_to_all_filtered_with_position : IAsyncLifetime, IDisposable {
		private readonly Fixture _fixture;
		private readonly IDisposable _loggingContext;

		public subscribe_to_all_filtered_with_position(ITestOutputHelper outputHelper) {
			_fixture = new Fixture();
			_loggingContext = LoggingHelper.Capture(outputHelper);
		}

		private async Task TestFilter(string streamPrefix, EventData[] events, IEventFilter filter) {
			var appeared = new TaskCompletionSource<bool>();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			using var enumerator = events.AsEnumerable().GetEnumerator();
			enumerator.MoveNext();

			var writeResult = await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
				AnyStreamRevision.NoStream, _fixture.CreateTestEvents(type: $"{streamPrefix}_{Guid.NewGuid():n}"));

			using var subscription = await _fixture.Client.SubscribeToAllAsync(writeResult.LogPosition, EventAppeared,
				false, SubscriptionDropped, filter);

			foreach (var e in events) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{e.EventId:n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			await appeared.Task.WithTimeout();

			Assert.False(dropped.Task.IsCompleted);

			subscription.Dispose();

			var (reason, ex) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.Disposed, reason);
			Assert.Null(ex);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				Serilog.Log.Verbose("EventAppeared received {eventId} {position}", e.OriginalEvent.EventId,
					e.OriginalPosition);
				if (writeResult.LogPosition >= e.OriginalEvent.Position) {
					appeared.TrySetException(new Exception(
						$"Expected to see events greater than {writeResult.LogPosition} but saw {e.OriginalEvent.Position}"));
					return Task.CompletedTask;
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

		[Fact]
		public async Task regular_expression_stream_name() {
			var streamPrefix = _fixture.GetStreamName();
			var filter = new StreamFilter(new RegularFilterExpression(new Regex($"^{streamPrefix}")));
			var events = _fixture.CreateTestEvents(10).ToArray();

			await TestFilter(streamPrefix, events, filter);
		}

		[Fact]
		public async Task prefix_stream_name() {
			var streamPrefix = _fixture.GetStreamName();
			var filter = new StreamFilter(new PrefixFilterExpression(streamPrefix));
			var events = _fixture.CreateTestEvents(10).ToArray();

			await TestFilter(streamPrefix, events, filter);
		}

		[Fact]
		public async Task regular_expression_event_type() {
			const string eventTypePrefix = nameof(regular_expression_event_type);
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(10)
				.Select(e =>
					new EventData(e.EventId, $"{eventTypePrefix}-{e.EventId:n}", e.Data, e.Metadata, e.IsJson))
				.ToArray();
			var filter = new EventTypeFilter(new RegularFilterExpression(new Regex($"^{eventTypePrefix}")));

			await TestFilter(streamPrefix, events, filter);
		}

		[Fact]
		public async Task prefix_event_type() {
			const string eventTypePrefix = nameof(prefix_event_type);
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(10)
				.Select(e =>
					new EventData(e.EventId, $"{eventTypePrefix}-{e.EventId:n}", e.Data, e.Metadata, e.IsJson))
				.ToArray();
			var filter = new EventTypeFilter(new PrefixFilterExpression(eventTypePrefix));

			await TestFilter(streamPrefix, events, filter);
		}

		public class Fixture : EventStoreGrpcFixture {
			public const string FilteredOutStream = nameof(FilteredOutStream);

			protected override Task Given() => Client.SetStreamMetadataAsync("$all", AnyStreamRevision.Any,
				new StreamMetadata(acl: new StreamAcl(SystemRoles.All)), TestCredentials.Root);

			protected override Task When() =>
				Client.AppendToStreamAsync(FilteredOutStream, AnyStreamRevision.NoStream, CreateTestEvents(10));
		}

		public Task InitializeAsync() => _fixture.InitializeAsync();

		public Task DisposeAsync() => _fixture.DisposeAsync();
		public void Dispose() => _loggingContext.Dispose();
	}
}
