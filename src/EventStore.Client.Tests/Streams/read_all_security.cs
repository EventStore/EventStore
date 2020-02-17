using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	public class read_all_security : IClassFixture<read_all_security.Fixture> {
		private readonly Fixture _fixture;

		public read_all_security(Fixture fixture) {
			_fixture = fixture;
		}

		public class Fixture : SecurityFixture {
			protected override Task When() => Task.CompletedTask;
		}

		[Fact]
		public async Task reading_all_with_not_existing_credentials_is_not_authenticated() {
			await Assert.ThrowsAsync<NotAuthenticatedException>(() => _fixture.ReadAllForward(TestCredentials.TestBadUser));
			await Assert.ThrowsAsync<NotAuthenticatedException>(() => _fixture.ReadAllBackward(TestCredentials.TestBadUser));
		}

		[Fact]
		public async Task reading_all_with_no_credentials_is_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadAllForward());
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadAllBackward());
		}

		[Fact]
		public async Task reading_all_with_not_authorized_user_credentials_is_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadAllForward(TestCredentials.TestUser2));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadAllBackward(TestCredentials.TestUser2));
		}

		[Fact]
		public async Task reading_all_with_authorized_user_credentials_succeeds() {
			await _fixture.ReadAllForward(TestCredentials.TestUser1);
			await _fixture.ReadAllBackward(TestCredentials.TestUser1);
		}

		[Fact]
		public async Task reading_all_with_admin_credentials_succeeds() {
			await _fixture.ReadAllForward(TestCredentials.TestAdmin);
			await _fixture.ReadAllBackward(TestCredentials.TestAdmin);
		}
	}
}
