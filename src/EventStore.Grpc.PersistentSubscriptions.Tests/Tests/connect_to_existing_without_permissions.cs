using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class connect_to_existing_without_permissions
		: IClassFixture<connect_to_existing_without_permissions.Fixture> {
		private const string Stream = "$" + nameof(connect_to_existing_without_permissions);
		private readonly Fixture _fixture;
		public connect_to_existing_without_permissions(Fixture fixture) { _fixture = fixture; }

		[Fact]
		public async Task throws_access_denied() {
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();
			_fixture.Client.PersistentSubscriptions.Subscribe(Stream, "agroupname55",
				delegate { return Task.CompletedTask; },
				(s, r, e) => dropped.TrySetResult((r,e)));

			var (reason, exception) = await dropped.Task.WithTimeout();
			Assert.Equal(SubscriptionDroppedReason.ServerError, reason);
			Assert.IsType<AccessDeniedException>(exception);
		}

		public class Fixture : EventStoreGrpcFixture {
			public Fixture() {
			}

			protected override Task Given() =>
				Client.PersistentSubscriptions.CreateAsync(
					Stream,
					"agroupname55",
					new PersistentSubscriptionSettings(),
					TestCredentials.Root);

			protected override Task When() => Task.CompletedTask;
		}
	}
}
