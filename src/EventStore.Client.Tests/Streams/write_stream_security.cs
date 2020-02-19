using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams
{
	public class write_stream_security : IClassFixture<write_stream_security.Fixture> {
		private readonly Fixture _fixture;

		public write_stream_security(Fixture fixture) {
			_fixture = fixture;
		}

		public class Fixture : SecurityFixture {
			protected override Task When() => Task.CompletedTask;
		}

		[Fact]
		public async Task writing_to_all_is_never_allowed() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream(SecurityFixture.AllStream));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream(SecurityFixture.AllStream, TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream(SecurityFixture.AllStream, TestCredentials.TestAdmin));
		}

		[Fact]
		public async Task writing_with_not_existing_credentials_is_not_authenticated() {
			await Assert.ThrowsAsync<NotAuthenticatedException>(() =>
				_fixture.AppendStream(SecurityFixture.WriteStream, TestCredentials.TestBadUser));
		}

		[Fact]
		public async Task writing_to_stream_with_no_credentials_is_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream(SecurityFixture.WriteStream));
		}

		[Fact]
		public async Task writing_to_stream_with_not_authorized_user_credentials_is_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream(SecurityFixture.WriteStream, TestCredentials.TestUser2));
		}

		[Fact]
		public async Task writing_to_stream_with_authorized_user_credentials_succeeds() {
			await _fixture.AppendStream(SecurityFixture.WriteStream, TestCredentials.TestUser1);
		}

		[Fact]
		public async Task writing_to_stream_with_admin_user_credentials_succeeds() {
			await _fixture.AppendStream(SecurityFixture.WriteStream, TestCredentials.TestAdmin);
		}


		[Fact]
		public async Task writing_to_no_acl_stream_succeeds_when_no_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NoAclStream);
		}

		[Fact]
		public async Task writing_to_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			await Assert.ThrowsAsync<NotAuthenticatedException>(() =>
				_fixture.AppendStream(SecurityFixture.NoAclStream, TestCredentials.TestBadUser));
		}

		[Fact]
		public async Task writing_to_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NoAclStream, TestCredentials.TestUser1);
			await _fixture.AppendStream(SecurityFixture.NoAclStream, TestCredentials.TestUser2);
		}

		[Fact]
		public async Task writing_to_no_acl_stream_succeeds_when_any_admin_user_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NoAclStream, TestCredentials.TestAdmin);
		}


		[Fact]
		public async Task writing_to_all_access_normal_stream_succeeds_when_no_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NormalAllStream);
		}

		[Fact]
		public async Task
			writing_to_all_access_normal_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			await Assert.ThrowsAsync<NotAuthenticatedException>(
				() => _fixture.AppendStream(SecurityFixture.NormalAllStream, TestCredentials.TestBadUser));
		}

		[Fact]
		public async Task writing_to_all_access_normal_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NormalAllStream, TestCredentials.TestUser1);
			await _fixture.AppendStream(SecurityFixture.NormalAllStream, TestCredentials.TestUser2);
		}

		[Fact]
		public async Task writing_to_all_access_normal_stream_succeeds_when_any_admin_user_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NormalAllStream, TestCredentials.TestAdmin);
		}
	}
}
