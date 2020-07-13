using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Services;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class subscribe_to_all_filtered : EventStoreClientAPITest, IAsyncLifetime {
		private readonly EventStoreClientAPIFixture _fixture;

		public subscribe_to_all_filtered(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, ClassData(typeof(StreamIdFilterCases))]
		public async Task stream_id_concurrently(bool useSsl, Func<string, Filter> getFilter, string name) {
			var streamName = $"{GetStreamName()}_{useSsl}_{name}";
			var eventAppearedSource1 = new TaskCompletionSource<ResolvedEvent>();
			var eventAppearedSource2 = new TaskCompletionSource<ResolvedEvent>();
			var connection = _fixture.Connections[useSsl];
			var filter = getFilter(streamName);

			using (await connection
				.FilteredSubscribeToAllAsync(false, filter, EventAppeared1, subscriptionDropped: SubscriptionDropped1)
				.WithTimeout())
			using (await connection
				.FilteredSubscribeToAllAsync(false, filter, EventAppeared2, subscriptionDropped: SubscriptionDropped2)
				.WithTimeout()) {
				var testEvents = _fixture.CreateTestEvents().ToArray();
				await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, testEvents).WithTimeout();

				var resolvedEvents =
					await Task.WhenAll(eventAppearedSource1.Task, eventAppearedSource2.Task).WithTimeout();

				Assert.Equal(testEvents[0].EventId, resolvedEvents[0].OriginalEvent.EventId);
				Assert.Equal(testEvents[0].EventId, resolvedEvents[1].OriginalEvent.EventId);
			}

			Task EventAppeared1(EventStoreSubscription s, ResolvedEvent e) {
				eventAppearedSource1.TrySetResult(e);

				return Task.CompletedTask;
			}

			Task EventAppeared2(EventStoreSubscription s, ResolvedEvent e) {
				eventAppearedSource2.TrySetResult(e);

				return Task.CompletedTask;
			}

			void SubscriptionDropped1(EventStoreSubscription s, SubscriptionDropReason reason, Exception ex) =>
				eventAppearedSource1.TrySetException(ex ?? new ObjectDisposedException(nameof(s)));

			void SubscriptionDropped2(EventStoreSubscription s, SubscriptionDropReason reason, Exception ex) =>
				eventAppearedSource2.TrySetException(ex ?? new ObjectDisposedException(nameof(s)));
		}

		[Theory, ClassData(typeof(EventTypeFilterCases))]
		public async Task event_type_concurrently(EventTypeFilterCases.Case testCase) {
			var eventTypePrefix = $"{GetStreamName()}_{testCase.UseSsl}_{testCase.FilterType}";

			var eventAppearedSource1 = new TaskCompletionSource<ResolvedEvent>();
			var eventAppearedSource2 = new TaskCompletionSource<ResolvedEvent>();
			var connection = _fixture.Connections[testCase.UseSsl];
			var filter = testCase.CreateFilter(eventTypePrefix);

			using (await connection
				.FilteredSubscribeToAllAsync(false, filter, EventAppeared1, subscriptionDropped: SubscriptionDropped1)
				.WithTimeout())
			using (await connection
				.FilteredSubscribeToAllAsync(false, filter, EventAppeared2, subscriptionDropped: SubscriptionDropped2)
				.WithTimeout()) {
				var testEvents = _fixture.CreateTestEvents(10)
					.Select(e =>
						new EventData(e.EventId, $"{eventTypePrefix}-{Guid.NewGuid():n}", e.IsJson, e.Data, e.Metadata))
					.ToArray();
				await connection.AppendToStreamAsync(Guid.NewGuid().ToString("n"), ExpectedVersion.NoStream, testEvents).WithTimeout();

				var resolvedEvents =
					await Task.WhenAll(eventAppearedSource1.Task, eventAppearedSource2.Task).WithTimeout();

				Assert.Equal(testEvents[0].EventId, resolvedEvents[0].OriginalEvent.EventId);
				Assert.Equal(testEvents[0].EventId, resolvedEvents[1].OriginalEvent.EventId);
			}

			Task EventAppeared1(EventStoreSubscription s, ResolvedEvent e) {
				eventAppearedSource1.TrySetResult(e);

				return Task.CompletedTask;
			}

			Task EventAppeared2(EventStoreSubscription s, ResolvedEvent e) {
				eventAppearedSource2.TrySetResult(e);

				return Task.CompletedTask;
			}

			void SubscriptionDropped1(EventStoreSubscription s, SubscriptionDropReason reason, Exception ex) =>
				eventAppearedSource1.TrySetException(ex ?? new ObjectDisposedException(nameof(s)));

			void SubscriptionDropped2(EventStoreSubscription s, SubscriptionDropReason reason, Exception ex) =>
				eventAppearedSource2.TrySetException(ex ?? new ObjectDisposedException(nameof(s)));
		}

		[Theory(Skip = nameof(drops_on_subscriber_error) + " is bugged"), MemberData(nameof(UseSslTestCases))]
		public async Task drops_on_subscriber_error(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			var droppedSource = new TaskCompletionSource<(SubscriptionDropReason, Exception)>();
			var expectedException = new Exception("subscriber error");
			var connection = _fixture.Connections[useSsl];

			using var _ = await connection
				.FilteredSubscribeToAllAsync(false, Filter.ExcludeSystemEvents, EventAppeared,
					subscriptionDropped: SubscriptionDropped).WithTimeout();

			var testEvents = _fixture.CreateTestEvents().ToArray();
			await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, testEvents).WithTimeout();

			var (dropped, exception) = await droppedSource.Task.WithTimeout();

			Assert.Equal(SubscriptionDropReason.EventHandlerException, dropped);
			Assert.IsType(expectedException.GetType(), exception);
			Assert.Equal(expectedException.Message, exception.Message);

			Task EventAppeared(EventStoreSubscription s, ResolvedEvent e) => Task.FromException(expectedException);

			void SubscriptionDropped(EventStoreSubscription s, SubscriptionDropReason reason, Exception ex) =>
				droppedSource.TrySetResult((reason, ex));
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task drops_on_unsubscribed(bool useSsl) {
			var droppedSource = new TaskCompletionSource<(SubscriptionDropReason, Exception)>();
			var connection = _fixture.Connections[useSsl];

			using var subscription = await connection
				.FilteredSubscribeToAllAsync(false, Filter.ExcludeSystemEvents, EventAppeared,
					subscriptionDropped: SubscriptionDropped).WithTimeout();

			subscription.Unsubscribe();

			var (dropped, exception) = await droppedSource.Task.WithTimeout();

			Assert.Equal(SubscriptionDropReason.UserInitiated, dropped);
			Assert.Null(exception);

			Task EventAppeared(EventStoreSubscription s, ResolvedEvent e)
				=> Task.CompletedTask;

			void SubscriptionDropped(EventStoreSubscription s, SubscriptionDropReason reason, Exception ex) =>
				droppedSource.TrySetResult((reason, ex));
		}

		public async Task InitializeAsync() {
			var connection = _fixture.Connections[false];
			;

			await connection.SetStreamMetadataAsync("$all", ExpectedVersion.Any,
				StreamMetadata.Build().SetReadRole(SystemRoles.All), DefaultUserCredentials.Admin).WithTimeout();
		}

		public async Task DisposeAsync() {
			var connection = _fixture.Connections[false];
			;

			await connection.SetStreamMetadataAsync("$all", ExpectedVersion.Any,
				StreamMetadata.Build().SetReadRole(null), DefaultUserCredentials.Admin).WithTimeout();
		}
	}
}
