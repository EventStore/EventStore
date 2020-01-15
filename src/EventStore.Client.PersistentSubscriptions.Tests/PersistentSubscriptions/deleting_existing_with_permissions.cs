using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.PersistentSubscriptions {
	public class deleting_existing_with_permissions
		: IClassFixture<deleting_existing_with_permissions.Fixture> {
		private const string Stream = nameof(deleting_existing_with_permissions);
		private readonly Fixture _fixture;

		public deleting_existing_with_permissions(Fixture fixture) {
			_fixture = fixture;
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;

			protected override Task When() =>
				Client.PersistentSubscriptions.CreateAsync(Stream, "groupname123",
					new PersistentSubscriptionSettings(),
					TestCredentials.Root);
		}

		[Fact]
		public Task the_delete_of_group_succeeds() =>
			_fixture.Client.PersistentSubscriptions.DeleteAsync(Stream, "groupname123",
				TestCredentials.Root);
	}
}
