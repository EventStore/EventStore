using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class update_existing
		: IClassFixture<update_existing.Fixture> {
		private const string Stream = nameof(update_existing);
		private const string Group = "existing";
		private readonly Fixture _fixture;

		public update_existing(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task the_completion_succeeds() {
			await _fixture.Client.PersistentSubscriptions.UpdateAsync(Stream, Group,
				new PersistentSubscriptionSettings(), TestCredentials.Root);
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override async Task Given() {
				await Client.AppendToStreamAsync(Stream, AnyStreamRevision.NoStream, CreateTestEvents());
				await Client.PersistentSubscriptions.CreateAsync(Stream, Group, new PersistentSubscriptionSettings(),
					TestCredentials.Root);
			}

			protected override Task When() => Task.CompletedTask;
		}
	}
}
