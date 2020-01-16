using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;

namespace EventStore.Client.Streams
{
	public class system_stream_security : IClassFixture<system_stream_security.Fixture> {
		private readonly Fixture _fixture;

		public system_stream_security(Fixture fixture) {
			_fixture = fixture;
		}

		public class Fixture : SecurityFixture {
			protected override Task When() => Task.CompletedTask;
		}

		[Fact]
		public async Task operations_on_system_stream_with_no_acl_set_fail_for_non_admin() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadEvent("$system-no-acl", TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamForward("$system-no-acl", TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamBackward("$system-no-acl", TestCredentials.TestUser1));

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream("$system-no-acl", TestCredentials.TestUser1));

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadMeta("$system-no-acl", TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.WriteMeta("$system-no-acl", TestCredentials.TestUser1, null));

			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.SubscribeToStream("$system-no-acl", TestCredentials.TestUser1));
		}

		[Fact]
		public async Task operations_on_system_stream_with_no_acl_set_succeed_for_admin() {
			await _fixture.AppendStream("$system-no-acl", TestCredentials.TestAdmin);

			await _fixture.ReadEvent("$system-no-acl", TestCredentials.TestAdmin);
			await _fixture.ReadStreamForward("$system-no-acl", TestCredentials.TestAdmin);
			await _fixture.ReadStreamBackward("$system-no-acl", TestCredentials.TestAdmin);

			await _fixture.ReadMeta("$system-no-acl", TestCredentials.TestAdmin);
			await _fixture.WriteMeta("$system-no-acl", TestCredentials.TestAdmin, null);

			await _fixture.SubscribeToStream("$system-no-acl", TestCredentials.TestAdmin);
		}

		[Fact]
		public async Task operations_on_system_stream_with_acl_set_to_usual_user_fail_for_not_authorized_user() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadEvent(SecurityFixture.SystemAclStream, TestCredentials.TestUser2));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadStreamForward(SecurityFixture.SystemAclStream, TestCredentials.TestUser2));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamBackward(SecurityFixture.SystemAclStream, TestCredentials.TestUser2));

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream(SecurityFixture.SystemAclStream, TestCredentials.TestUser2));

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadMeta(SecurityFixture.SystemAclStream, TestCredentials.TestUser2));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.WriteMeta(SecurityFixture.SystemAclStream, TestCredentials.TestUser2, TestCredentials.TestUser1.Username));

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.SubscribeToStream(SecurityFixture.SystemAclStream, TestCredentials.TestUser2));
		}

		[Fact]
		public async Task operations_on_system_stream_with_acl_set_to_usual_user_succeed_for_that_user() {
			await _fixture.AppendStream(SecurityFixture.SystemAclStream, TestCredentials.TestUser1);
			await _fixture.ReadEvent(SecurityFixture.SystemAclStream, TestCredentials.TestUser1);
			await _fixture.ReadStreamForward(SecurityFixture.SystemAclStream, TestCredentials.TestUser1);
			await _fixture.ReadStreamBackward(SecurityFixture.SystemAclStream, TestCredentials.TestUser1);

			await _fixture.ReadMeta(SecurityFixture.SystemAclStream, TestCredentials.TestUser1);
			await _fixture.WriteMeta(SecurityFixture.SystemAclStream, TestCredentials.TestUser1, TestCredentials.TestUser1.Username);

			await _fixture.SubscribeToStream(SecurityFixture.SystemAclStream, TestCredentials.TestUser1);
		}

		[Fact]
		public async Task operations_on_system_stream_with_acl_set_to_usual_user_succeed_for_admin() {
			await _fixture.AppendStream(SecurityFixture.SystemAclStream, TestCredentials.TestAdmin);
			await _fixture.ReadEvent(SecurityFixture.SystemAclStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamForward(SecurityFixture.SystemAclStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamBackward(SecurityFixture.SystemAclStream, TestCredentials.TestAdmin);
			
			await _fixture.ReadMeta(SecurityFixture.SystemAclStream, TestCredentials.TestAdmin);
			await _fixture.WriteMeta(SecurityFixture.SystemAclStream, TestCredentials.TestAdmin, TestCredentials.TestUser1.Username);

			await _fixture.SubscribeToStream(SecurityFixture.SystemAclStream, TestCredentials.TestAdmin);
		}


		[Fact]
		public async Task operations_on_system_stream_with_acl_set_to_admins_fail_for_usual_user() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadEvent(SecurityFixture.SystemAdminStream, TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadStreamForward(SecurityFixture.SystemAdminStream, TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadStreamBackward(SecurityFixture.SystemAdminStream, TestCredentials.TestUser1));

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream(SecurityFixture.SystemAdminStream, TestCredentials.TestUser1));

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadMeta(SecurityFixture.SystemAdminStream, TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.WriteMeta(SecurityFixture.SystemAdminStream, TestCredentials.TestUser1, SystemRoles.Admins));

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.SubscribeToStream(SecurityFixture.SystemAdminStream, TestCredentials.TestUser1));
		}

		[Fact]
		public async Task operations_on_system_stream_with_acl_set_to_admins_succeed_for_admin() {
			await _fixture.AppendStream(SecurityFixture.SystemAdminStream, TestCredentials.TestAdmin);
			await _fixture.ReadEvent(SecurityFixture.SystemAdminStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamForward(SecurityFixture.SystemAdminStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamBackward(SecurityFixture.SystemAdminStream, TestCredentials.TestAdmin);

			await _fixture.ReadMeta(SecurityFixture.SystemAdminStream, TestCredentials.TestAdmin);
			await _fixture.WriteMeta(SecurityFixture.SystemAdminStream, TestCredentials.TestAdmin, SystemRoles.Admins);

			await _fixture.SubscribeToStream(SecurityFixture.SystemAdminStream, TestCredentials.TestAdmin);
		}


		[Fact]
		public async Task operations_on_system_stream_with_acl_set_to_all_succeed_for_not_authenticated_user() {
			await _fixture.AppendStream(SecurityFixture.SystemAllStream);
			await _fixture.ReadEvent(SecurityFixture.SystemAllStream);
			await _fixture.ReadStreamForward(SecurityFixture.SystemAllStream);
			await _fixture.ReadStreamBackward(SecurityFixture.SystemAllStream);

			await _fixture.ReadMeta(SecurityFixture.SystemAllStream);
			await _fixture.WriteMeta(SecurityFixture.SystemAllStream, role: SystemRoles.All);

			await _fixture.SubscribeToStream(SecurityFixture.SystemAllStream);
		}

		[Fact]
		public async Task operations_on_system_stream_with_acl_set_to_all_succeed_for_usual_user() {
			await _fixture.AppendStream(SecurityFixture.SystemAllStream, TestCredentials.TestUser1);
			await _fixture.ReadEvent(SecurityFixture.SystemAllStream, TestCredentials.TestUser1);
			await _fixture.ReadStreamForward(SecurityFixture.SystemAllStream, TestCredentials.TestUser1);
			await _fixture.ReadStreamBackward(SecurityFixture.SystemAllStream, TestCredentials.TestUser1);

			await _fixture.ReadMeta(SecurityFixture.SystemAllStream, TestCredentials.TestUser1);
			await _fixture.WriteMeta(SecurityFixture.SystemAllStream, TestCredentials.TestUser1, SystemRoles.All);

			await _fixture.SubscribeToStream(SecurityFixture.SystemAllStream, TestCredentials.TestUser1);
		}

		[Fact]
		public async Task operations_on_system_stream_with_acl_set_to_all_succeed_for_admin() {
			await _fixture.AppendStream(SecurityFixture.SystemAllStream, TestCredentials.TestAdmin);
			await _fixture.ReadEvent(SecurityFixture.SystemAllStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamForward(SecurityFixture.SystemAllStream, TestCredentials.TestAdmin);
			await _fixture.ReadStreamBackward(SecurityFixture.SystemAllStream, TestCredentials.TestAdmin);

			await _fixture.ReadMeta(SecurityFixture.SystemAllStream, TestCredentials.TestAdmin);
			await _fixture.WriteMeta(SecurityFixture.SystemAllStream, TestCredentials.TestAdmin, SystemRoles.All);

			await _fixture.SubscribeToStream(SecurityFixture.SystemAllStream, TestCredentials.TestAdmin);
		}
	}
}
