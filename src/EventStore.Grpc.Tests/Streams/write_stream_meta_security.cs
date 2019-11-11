using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;

namespace EventStore.Grpc.Streams {
	public class write_stream_meta_security : IClassFixture<write_stream_meta_security.Fixture> {
		private readonly Fixture _fixture;

		public write_stream_meta_security(Fixture fixture) {
			_fixture = fixture;
		}

		public class Fixture : SecurityFixture {
			protected override Task When() => Task.CompletedTask;
		}

		[Fact]
		public async Task writing_meta_with_not_existing_credentials_is_not_authenticated() {
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.WriteMeta(SecurityFixture.MetaWriteStream, TestCredentials.TestBadUser, TestCredentials.TestUser1.Username));
		}

		[Fact]
		public async Task writing_meta_to_stream_with_no_credentials_is_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.WriteMeta(SecurityFixture.MetaWriteStream, role: TestCredentials.TestUser1.Username));
		}

		[Fact]
		public async Task writing_meta_to_stream_with_not_authorized_user_credentials_is_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.WriteMeta(SecurityFixture.MetaWriteStream, TestCredentials.TestUser2, TestCredentials.TestUser1.Username));
		}

		[Fact]
		public async Task writing_meta_to_stream_with_authorized_user_credentials_succeeds() {
			await _fixture.WriteMeta(SecurityFixture.MetaWriteStream, TestCredentials.TestUser1, TestCredentials.TestUser1.Username);
		}

		[Fact]
		public async Task writing_meta_to_stream_with_admin_user_credentials_succeeds() {
			await _fixture.WriteMeta(SecurityFixture.MetaWriteStream, TestCredentials.TestAdmin, TestCredentials.TestUser1.Username);
		}


		[Fact]
		public async Task writing_meta_to_no_acl_stream_succeeds_when_no_credentials_are_passed() {
			await _fixture.WriteMeta(SecurityFixture.NoAclStream);
		}

		[Fact]
		public async Task
			writing_meta_to_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.WriteMeta(SecurityFixture.NoAclStream, TestCredentials.TestBadUser, null));
		}

		[Fact]
		public async Task writing_meta_to_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			await _fixture.WriteMeta(SecurityFixture.NoAclStream, TestCredentials.TestUser1, null);
			await _fixture.WriteMeta(SecurityFixture.NoAclStream, TestCredentials.TestUser2, null);
		}

		[Fact]
		public async Task writing_meta_to_no_acl_stream_succeeds_when_admin_user_credentials_are_passed() {
			await _fixture.WriteMeta(SecurityFixture.NoAclStream, TestCredentials.TestAdmin, null);
		}


		[Fact]
		public async Task writing_meta_to_all_access_normal_stream_succeeds_when_no_credentials_are_passed() {
			await _fixture.WriteMeta(SecurityFixture.NormalAllStream, role: SystemRoles.All);
		}

		[Fact]
		public async Task
			writing_meta_to_all_access_normal_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.WriteMeta(SecurityFixture.NormalAllStream, TestCredentials.TestBadUser, SystemRoles.All));
		}

		[Fact]
		public async Task
			writing_meta_to_all_access_normal_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			await _fixture.WriteMeta(SecurityFixture.NormalAllStream, TestCredentials.TestUser1, SystemRoles.All);
			await _fixture.WriteMeta(SecurityFixture.NormalAllStream, TestCredentials.TestUser2, SystemRoles.All);
		}

		[Fact]
		public async Task writing_meta_to_all_access_normal_stream_succeeds_when_admin_user_credentials_are_passed() {
			await _fixture.WriteMeta(SecurityFixture.NormalAllStream, TestCredentials.TestAdmin, SystemRoles.All);
		}
	}
}
