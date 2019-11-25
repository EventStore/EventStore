using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class happy_case_writing_and_subscribing_to_normal_events_manual_ack
		: IClassFixture<happy_case_writing_and_subscribing_to_normal_events_manual_ack.Fixture> {
		private const string Stream = nameof(happy_case_writing_and_subscribing_to_normal_events_manual_ack);
		private const string Group = nameof(Group);
		private const int BufferCount = 10;
		private const int EventWriteCount = BufferCount * 2;

		private readonly Fixture _fixture;

		public happy_case_writing_and_subscribing_to_normal_events_manual_ack(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task Test() {
			await _fixture.EventsReceived.WithTimeout();
		}

		public class Fixture : EventStoreGrpcFixture {
			private readonly EventData[] _events;
			private readonly TaskCompletionSource<bool> _eventsReceived;
			public Task EventsReceived => _eventsReceived.Task;

			private PersistentSubscription _subscription;
			private int _eventReceivedCount;

			public Fixture() {
				_events = CreateTestEvents(EventWriteCount).ToArray();
				_eventsReceived = new TaskCompletionSource<bool>();
			}

			protected override async Task Given() {
				await Client.PersistentSubscriptions.CreateAsync(Stream, Group,
					new PersistentSubscriptionSettings(startFrom: StreamRevision.End, resolveLinkTos: true),
					TestCredentials.Root);
				_subscription = Client.PersistentSubscriptions.Subscribe(Stream, Group,
					(subscription, e, retryCount, ct) => {
						subscription.Ack(e);
						if (Interlocked.Increment(ref _eventReceivedCount) == _events.Length) {
							_eventsReceived.TrySetResult(true);
						}

						return Task.CompletedTask;
					}, (s, r, e) => {
						if (e != null) {
							_eventsReceived.TrySetException(e);
						}
					}, autoAck: false,
					bufferSize: BufferCount,
					userCredentials: TestCredentials.Root);
			}

			protected override async Task When() {
				foreach (var e in _events) {
					await Client.AppendToStreamAsync(Stream, AnyStreamRevision.Any, new[] {e});
				}
			}

			public override Task DisposeAsync() {
				_subscription?.Dispose();
				return base.DisposeAsync();
			}
		}
	}
}
