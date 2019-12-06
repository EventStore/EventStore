using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class create_without_permissions
		: IClassFixture<create_without_permissions.Fixture> {
		public create_without_permissions(Fixture fixture) {
			_fixture = fixture;
		}

		private const string Stream = nameof(create_without_permissions);
		private readonly Fixture _fixture;

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}

		[Fact]
		public Task the_completion_fails_with_access_denied() =>
			Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.Client.PersistentSubscriptions.CreateAsync(Stream, "group57",
					new PersistentSubscriptionSettings()));
	}
}
