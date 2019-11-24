using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class deleting_without_permissions
		: IClassFixture<deleting_without_permissions.Fixture> {
		private readonly Fixture _fixture;
		private const string Stream = nameof(deleting_without_permissions);

		public deleting_without_permissions(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task the_delete_fails_with_access_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(
				() => _fixture.Client.PersistentSubscriptions.DeleteAsync(Stream,
					Guid.NewGuid().ToString()));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
