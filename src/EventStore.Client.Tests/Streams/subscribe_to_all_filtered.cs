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
	public class subscribe_to_all_filtered : IClassFixture<subscribe_to_all_filtered.Fixture>, IAsyncLifetime, IDisposable {
		private readonly Fixture _fixture;
		private readonly IDisposable _loggingContext;

		public subscribe_to_all_filtered(ITestOutputHelper outputHelper) {
			_fixture = new Fixture();
			_loggingContext = LoggingHelper.Capture(outputHelper);
		}

		[Fact]
		public async Task regular_expression_stream_name() {
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(20).ToArray();
			var beforeEvents = events.Take(10).ToArray();
			var afterEvents = events.Skip(10).ToArray();
			var result = new List<EventRecord>();
			var source = new TaskCompletionSource<bool>();

			foreach (var e in beforeEvents) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			await _fixture.Client.SubscribeToAllAsync(EventAppeared,
				false, filter: new StreamFilter(new RegularFilterExpression(new Regex($"^{streamPrefix}"))));

			foreach (var e in afterEvents) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			Task EventAppeared(StreamSubscription _, ResolvedEvent e, CancellationToken ct) {
				result.Add(e.Event);
				if (result.Count >= events.Length) {
					source.TrySetResult(true);
				}

				return Task.CompletedTask;
			}

			await source.Task.WithTimeout();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.EventId));
		}

		[Fact]
		public async Task prefix_stream_name() {
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(20).ToArray();
			var beforeEvents = events.Take(10).ToArray();
			var afterEvents = events.Skip(10).ToArray();
			var result = new List<EventRecord>();
			var source = new TaskCompletionSource<bool>();

			foreach (var e in beforeEvents) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			await _fixture.Client.SubscribeToAllAsync(EventAppeared,
				false, filter: new StreamFilter(new PrefixFilterExpression(streamPrefix)));

			foreach (var e in afterEvents) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			Task EventAppeared(StreamSubscription _, ResolvedEvent e, CancellationToken ct) {
				result.Add(e.Event);
				if (result.Count >= events.Length) {
					source.TrySetResult(true);
				}

				return Task.CompletedTask;
			}

			await source.Task.WithTimeout();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.EventId));
		}

		[Fact]
		public async Task regular_expression_event_type() {
			const string eventTypePrefix = nameof(regular_expression_event_type);
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(20)
				.Select(e =>
					new EventData(e.EventId, $"{eventTypePrefix}-{Guid.NewGuid():n}", e.Data, e.Metadata, e.IsJson))
				.ToArray();
			var beforeEvents = events.Take(10);
			var afterEvents = events.Skip(10);
			var result = new List<EventRecord>();
			var source = new TaskCompletionSource<bool>();

			foreach (var e in beforeEvents) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			await _fixture.Client.SubscribeToAllAsync(EventAppeared,
				false, filter: new EventTypeFilter(new RegularFilterExpression(new Regex($"^{eventTypePrefix}"))));

			foreach (var e in afterEvents) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			Task EventAppeared(StreamSubscription _, ResolvedEvent e, CancellationToken ct) {
				result.Add(e.Event);
				if (result.Count >= events.Length) {
					source.TrySetResult(true);
				}

				return Task.CompletedTask;
			}

			await source.Task.WithTimeout();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.EventId));
		}

		[Fact]
		public async Task prefix_event_type() {
			const string eventTypePrefix = nameof(prefix_event_type);
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(20)
				.Select(e =>
					new EventData(e.EventId, $"{eventTypePrefix}-{Guid.NewGuid():n}", e.Data, e.Metadata, e.IsJson))
				.ToArray();
			var beforeEvents = events.Take(10);
			var afterEvents = events.Skip(10);
			var result = new List<EventRecord>();
			var source = new TaskCompletionSource<bool>();

			foreach (var e in beforeEvents) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			await _fixture.Client.SubscribeToAllAsync(EventAppeared,
				false, filter: new EventTypeFilter(new PrefixFilterExpression(eventTypePrefix)));

			foreach (var e in afterEvents) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			Task EventAppeared(StreamSubscription _, ResolvedEvent e, CancellationToken ct) {
				result.Add(e.Event);
				if (result.Count >= events.Length) {
					source.TrySetResult(true);
				}

				return Task.CompletedTask;
			}

			await source.Task.WithTimeout();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.EventId));
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
