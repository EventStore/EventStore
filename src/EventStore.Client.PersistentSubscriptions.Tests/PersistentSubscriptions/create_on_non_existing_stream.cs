using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.PersistentSubscriptions {
	public class create_on_non_existing_stream
		: IClassFixture<create_on_non_existing_stream.Fixture> {
		public create_on_non_existing_stream(Fixture fixture) {
			_fixture = fixture;
		}

		private const string Stream = nameof(create_on_non_existing_stream);
		private readonly Fixture _fixture;

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}

		[Fact]
		public async Task the_completion_succeeds() {
			await _fixture.Client.PersistentSubscriptions.CreateAsync(Stream, "nonexistinggroup",
				new PersistentSubscriptionSettings(),
				TestCredentials.Root);
		}
	}
}
