using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Users {
	public class getting_current_user : IClassFixture<getting_current_user.Fixture> {
		private readonly Fixture _fixture;

		public getting_current_user(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task returns_the_current_user() {
			var user = await _fixture.Client.UsersManager.GetCurrentUserAsync(TestCredentials.Root);
			Assert.Equal(TestCredentials.Root.Username, user.LoginName);
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
