using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;

namespace EventStore.Grpc.Streams {
	public class subscribe_to_all_filtered : IClassFixture<subscribe_to_all_filtered.Fixture> {
		private readonly Fixture _fixture;

		public subscribe_to_all_filtered(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task regular_expression_stream_name() {
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(10).ToArray();
			var result = new List<ResolvedEvent>();
			var source = new TaskCompletionSource<bool>();

			_fixture.Client.SubscribeToAll(EventAppeared,
				false, filter: new StreamFilter(new RegularFilterExpression(new Regex($"^{streamPrefix}"))));

			foreach (var e in events) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			Task EventAppeared(StreamSubscription _, ResolvedEvent e, CancellationToken ct) {
				result.Add(e);
				if (result.Count >= events.Length) {
					source.TrySetResult(true);
				}

				return Task.CompletedTask;
			}

			await source.Task.WithTimeout();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.OriginalEvent.EventId));
		}

		[Fact]
		public async Task prefix_stream_name() {
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(10).ToArray();
			var result = new List<ResolvedEvent>();
			var source = new TaskCompletionSource<bool>();

			_fixture.Client.SubscribeToAll(EventAppeared,
				false, filter: new StreamFilter(new PrefixFilterExpression(streamPrefix)));

			foreach (var e in events) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			Task EventAppeared(StreamSubscription _, ResolvedEvent e, CancellationToken ct) {
				result.Add(e);
				if (result.Count >= events.Length) {
					source.TrySetResult(true);
				}

				return Task.CompletedTask;
			}

			await source.Task.WithTimeout();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.OriginalEvent.EventId));
		}

		[Fact]
		public async Task regular_expression_event_type() {
			const string eventTypePrefix = nameof(regular_expression_event_type);
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(10)
				.Select(e =>
					new EventData(e.EventId, $"{eventTypePrefix}-{Guid.NewGuid():n}", e.Data, e.Metadata, e.IsJson))
				.ToArray();
			var result = new List<ResolvedEvent>();
			var source = new TaskCompletionSource<bool>();

			_fixture.Client.SubscribeToAll(EventAppeared,
				false, filter: new EventTypeFilter(new RegularFilterExpression(new Regex($"^{eventTypePrefix}"))));

			foreach (var e in events) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			Task EventAppeared(StreamSubscription _, ResolvedEvent e, CancellationToken ct) {
				result.Add(e);
				if (result.Count >= events.Length) {
					source.TrySetResult(true);
				}

				return Task.CompletedTask;
			}

			await source.Task.WithTimeout();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.OriginalEvent.EventId));
		}

		[Fact]
		public async Task prefix_event_type() {
			const string eventTypePrefix = nameof(prefix_event_type);
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(10)
				.Select(e =>
					new EventData(e.EventId, $"{eventTypePrefix}-{Guid.NewGuid():n}", e.Data, e.Metadata, e.IsJson))
				.ToArray();
			var result = new List<ResolvedEvent>();
			var source = new TaskCompletionSource<bool>();

			_fixture.Client.SubscribeToAll(EventAppeared,
				false, filter: new EventTypeFilter(new PrefixFilterExpression(eventTypePrefix)));

			foreach (var e in events) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] {e});
			}

			Task EventAppeared(StreamSubscription _, ResolvedEvent e, CancellationToken ct) {
				result.Add(e);
				if (result.Count >= events.Length) {
					source.TrySetResult(true);
				}

				return Task.CompletedTask;
			}

			await source.Task.WithTimeout();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.OriginalEvent.EventId));
		}

		public class Fixture : EventStoreGrpcFixture {
			public const string FilteredOutStream = nameof(FilteredOutStream);

			protected override Task Given() => Client.SetStreamMetadataAsync("$all", AnyStreamRevision.Any,
				new StreamMetadata(acl: new StreamAcl(SystemRoles.All)), TestCredentials.Root);

			protected override Task When() =>
				Client.AppendToStreamAsync(FilteredOutStream, AnyStreamRevision.NoStream, CreateTestEvents(10));
		}
	}
}
