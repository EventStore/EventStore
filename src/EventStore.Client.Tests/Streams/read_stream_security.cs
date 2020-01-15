using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	public class read_stream_security : IClassFixture<read_stream_security.Fixture> {
		private readonly Fixture _fixture;

		public read_stream_security(Fixture fixture) {
			_fixture = fixture;
		}

		public class Fixture : SecurityFixture {
			protected override Task When() => Task.CompletedTask;
		}

		[Fact]
		public async Task reading_stream_with_not_existing_credentials_is_not_authenticated() {
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadEvent(SecurityFixture.ReadStream, TestCredentials.TestBadUser));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamForward(SecurityFixture.ReadStream, TestCredentials.TestBadUser));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamBackward(SecurityFixture.ReadStream, TestCredentials.TestBadUser));
		}

		[Fact]
		public async Task reading_stream_with_no_credentials_is_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadEvent(SecurityFixture.ReadStream));
			await Assert.ThrowsAsync<AccessDeniedException>(
				() => _fixture.ReadStreamForward(SecurityFixture.ReadStream));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamBackward(SecurityFixture.ReadStream));
		}

		[Fact]
		public async Task reading_stream_with_not_authorized_user_credentials_is_denied() {
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadEvent(SecurityFixture.ReadStream, TestCredentials.TestUser2));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamForward(SecurityFixture.ReadStream, TestCredentials.TestUser2));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamBackward(SecurityFixture.ReadStream, TestCredentials.TestUser2));
		}

		[Fact]
		public async Task reading_stream_with_authorized_user_credentials_succeeds() {
			await _fixture.AppendStream(SecurityFixture.ReadStream, TestCredentials.TestUser1);

			await _fixture.ReadEvent(SecurityFixture.ReadStream, TestCredentials.TestUser1);
			await _fixture.ReadStreamForward(SecurityFixture.ReadStream, TestCredentials.TestUser1);
			await _fixture.ReadStreamBackward(SecurityFixture.ReadStream, TestCredentials.TestUser1);
		}

		[Fact]
		public async Task reading_stream_with_admin_user_credentials_succeeds() {
			await _fixture.AppendStream(SecurityFixture.ReadStream, TestCredentials.TestAdmin);

			await _fixture.ReadEvent(SecurityFixture.ReadStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamForward(SecurityFixture.ReadStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamBackward(SecurityFixture.ReadStream, TestCredentials.TestAdmin);
		}


		[Fact]
		public async Task reading_no_acl_stream_succeeds_when_no_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NoAclStream);

			await _fixture.ReadEvent(SecurityFixture.NoAclStream);
			await _fixture.ReadStreamForward(SecurityFixture.NoAclStream);
			await _fixture.ReadStreamBackward(SecurityFixture.NoAclStream);
		}

		[Fact]
		public async Task reading_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			await Assert.ThrowsAsync<AccessDeniedException>(
				() => _fixture.ReadEvent(SecurityFixture.NoAclStream, TestCredentials.TestBadUser));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamForward(SecurityFixture.NoAclStream, TestCredentials.TestBadUser));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamBackward(SecurityFixture.NoAclStream, TestCredentials.TestBadUser));
		}

		[Fact]
		public async Task reading_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NoAclStream, TestCredentials.TestUser1);

			await _fixture.ReadEvent(SecurityFixture.NoAclStream, TestCredentials.TestUser1);
			await _fixture.ReadStreamForward(SecurityFixture.NoAclStream, TestCredentials.TestUser1);
			await _fixture.ReadStreamBackward(SecurityFixture.NoAclStream, TestCredentials.TestUser1);
			await _fixture.ReadEvent(SecurityFixture.NoAclStream, TestCredentials.TestUser2);
			await _fixture.ReadStreamForward(SecurityFixture.NoAclStream, TestCredentials.TestUser2);
			await _fixture.ReadStreamBackward(SecurityFixture.NoAclStream, TestCredentials.TestUser2);
		}

		[Fact]
		public async Task reading_no_acl_stream_succeeds_when_admin_user_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NoAclStream, TestCredentials.TestAdmin);
			await _fixture.ReadEvent(SecurityFixture.NoAclStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamForward(SecurityFixture.NoAclStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamBackward(SecurityFixture.NoAclStream, TestCredentials.TestAdmin);
		}


		[Fact]
		public async Task reading_all_access_normal_stream_succeeds_when_no_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NormalAllStream);
			await _fixture.ReadEvent(SecurityFixture.NormalAllStream);
			await _fixture.ReadStreamForward(SecurityFixture.NormalAllStream);
			await _fixture.ReadStreamBackward(SecurityFixture.NormalAllStream);
		}

		[Fact]
		public async Task
			reading_all_access_normal_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadEvent(SecurityFixture.NormalAllStream, TestCredentials.TestBadUser));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamForward(SecurityFixture.NormalAllStream, TestCredentials.TestBadUser));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamBackward(SecurityFixture.NormalAllStream, TestCredentials.TestBadUser));
		}

		[Fact]
		public async Task reading_all_access_normal_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NormalAllStream, TestCredentials.TestUser1);
			await _fixture.ReadEvent(SecurityFixture.NormalAllStream, TestCredentials.TestUser1);
			await _fixture.ReadStreamForward(SecurityFixture.NormalAllStream, TestCredentials.TestUser1);
			await _fixture.ReadStreamBackward(SecurityFixture.NormalAllStream, TestCredentials.TestUser1);
			await _fixture.ReadEvent(SecurityFixture.NormalAllStream, TestCredentials.TestUser2);
			await _fixture.ReadStreamForward(SecurityFixture.NormalAllStream, TestCredentials.TestUser2);
			await _fixture.ReadStreamBackward(SecurityFixture.NormalAllStream, TestCredentials.TestUser2);
		}

		[Fact]
		public async Task reading_all_access_normal_stream_succeeds_when_admin_user_credentials_are_passed() {
			await _fixture.AppendStream(SecurityFixture.NormalAllStream, TestCredentials.TestAdmin);
			await _fixture.ReadEvent(SecurityFixture.NormalAllStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamForward(SecurityFixture.NormalAllStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamBackward(SecurityFixture.NormalAllStream, TestCredentials.TestAdmin);
		}
	}
}
