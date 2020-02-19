using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	public class subscribe_to_all_security : IClassFixture<subscribe_to_all_security.Fixture> {
		private readonly Fixture _fixture;

		public subscribe_to_all_security(Fixture fixture) {
			_fixture = fixture;
		}

		public class Fixture : SecurityFixture {
			protected override Task When() => Task.CompletedTask;
		}

		[Fact]
		public async Task subscribing_to_all_with_not_existing_credentials_is_not_authenticated() {
			await Assert.ThrowsAsync<NotAuthenticatedException>(() => _fixture.SubscribeToAll(TestCredentials.TestBadUser));
		}

		[Fact]
		public async Task subscribing_to_all_with_no_credentials_is_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.SubscribeToAll());
		}

		[Fact]
		public async Task subscribing_to_all_with_not_authorized_user_credentials_is_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.SubscribeToAll(TestCredentials.TestUser2));
		}

		[Fact]
		public async Task subscribing_to_all_with_authorized_user_credentials_succeeds() {
			await _fixture.SubscribeToAll(TestCredentials.TestUser1);
		}

		[Fact]
		public async Task subscribing_to_all_with_admin_user_credentials_succeeds() {
			await _fixture.SubscribeToAll(TestCredentials.TestAdmin);
		}
	}
}
