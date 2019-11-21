using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class connect_to_existing_persistent_subscription_with_max_one_client
		: IClassFixture<connect_to_existing_persistent_subscription_with_max_one_client.Fixture> {
		private const string Group = "startinbeginning1";
		private const string Stream = nameof(connect_to_existing_persistent_subscription_with_max_one_client);
		private readonly Fixture _fixture;

		public connect_to_existing_persistent_subscription_with_max_one_client(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task the_second_subscription_fails_to_connect() {
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();

			using var first = _fixture.Client.PersistentSubscriptions
				.Subscribe(Stream, Group, delegate { return Task.CompletedTask; });
			await first.Started.WithTimeout();
			using var second = _fixture.Client.PersistentSubscriptions
				.Subscribe(Stream, Group, delegate { return Task.CompletedTask; },
					(s, r, e) => dropped.SetResult((r, e)));
			await second.Started.WithTimeout();

			var (reason, exception) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.ServerError, reason);
			var ex = Assert.IsType<MaximumSubscribersReachedException>(exception);
			Assert.Equal(Stream, ex.StreamName);
			Assert.Equal(Group, ex.GroupName);
		}

		public class Fixture : EventStoreGrpcFixture {
			public Fixture() {
			}

			protected override Task Given() =>
				Client.PersistentSubscriptions.CreateAsync(
					Stream,
					Group,
					new PersistentSubscriptionSettings(maxSubscriberCount: 1),
					TestCredentials.Root);

			protected override Task When() => Task.CompletedTask;
		}
	}
}
