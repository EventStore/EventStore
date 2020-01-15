using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.PersistentSubscriptions {
	public class create_after_deleting_the_same
		: IClassFixture<create_after_deleting_the_same.Fixture> {
		public create_after_deleting_the_same(Fixture fixture) {
			_fixture = fixture;
		}

		private const string Stream = nameof(create_after_deleting_the_same);
		private readonly Fixture _fixture;

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;

			protected override async Task When() {
				await Client.AppendToStreamAsync(Stream, AnyStreamRevision.Any, CreateTestEvents());
				await Client.PersistentSubscriptions.CreateAsync(Stream, "existing",
					new PersistentSubscriptionSettings(), TestCredentials.Root);
				await Client.PersistentSubscriptions.DeleteAsync(Stream, "existing",
					TestCredentials.Root);
			}
		}

		[Fact]
		public async Task the_completion_succeeds() {
			await _fixture.Client.PersistentSubscriptions.CreateAsync(Stream, "existing",
				new PersistentSubscriptionSettings(), TestCredentials.Root);
		}
	}
}
