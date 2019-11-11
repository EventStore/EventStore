using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;

namespace EventStore.Grpc.Streams {
	public class stream_security_inheritance : IClassFixture<stream_security_inheritance.Fixture> {
		private readonly Fixture _fixture;

		public stream_security_inheritance(Fixture fixture) {
			_fixture = fixture;
		}

		public class Fixture : SecurityFixture {
			protected override async Task When() {
				var settings = new SystemSettings(userStreamAcl: new StreamAcl(writeRole: "user1"),
					systemStreamAcl: new StreamAcl(writeRole: "user1"));
				await Client.SetSystemSettingsAsync(settings, TestCredentials.TestAdmin);

				await Client.SetStreamMetadataAsync("user-no-acl", AnyStreamRevision.NoStream,
					new StreamMetadata(), TestCredentials.TestAdmin);
				await Client.SetStreamMetadataAsync("user-w-diff", AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(writeRole: "user2")), TestCredentials.TestAdmin);
				await Client.SetStreamMetadataAsync("user-w-multiple", AnyStreamRevision.NoStream,
						new StreamMetadata(acl: new StreamAcl(writeRoles: new[] {"user1", "user2"})),
						TestCredentials.TestAdmin);
				await Client.SetStreamMetadataAsync("user-w-restricted", AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(writeRoles: new string[0])),
					TestCredentials.TestAdmin);
				await Client.SetStreamMetadataAsync("user-w-all", AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(writeRole: SystemRoles.All)),
					TestCredentials.TestAdmin);

				await Client.SetStreamMetadataAsync("user-r-restricted", AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl("user1")), TestCredentials.TestAdmin);

				await Client.SetStreamMetadataAsync("$sys-no-acl", AnyStreamRevision.NoStream,
					new StreamMetadata(), TestCredentials.TestAdmin);
				await Client.SetStreamMetadataAsync("$sys-w-diff", AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(writeRole: "user2")), TestCredentials.TestAdmin);
				await Client.SetStreamMetadataAsync("$sys-w-multiple", AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(writeRoles: new[] {"user1", "user2"})),
					TestCredentials.TestAdmin);

				await Client.SetStreamMetadataAsync("$sys-w-restricted", AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(writeRoles: new string[0])),
					TestCredentials.TestAdmin);
				await Client.SetStreamMetadataAsync("$sys-w-all", AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(writeRole: SystemRoles.All)),
					TestCredentials.TestAdmin);
			}
		}


		[Fact]
		public async Task acl_inheritance_is_working_properly_on_user_streams() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream("user-no-acl"));
			await _fixture.AppendStream("user-no-acl", TestCredentials.TestUser1);
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.AppendStream("user-no-acl", TestCredentials.TestUser2));
			await _fixture.AppendStream("user-no-acl", TestCredentials.TestAdmin);

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream("user-w-diff"));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.AppendStream("user-w-diff", TestCredentials.TestUser1));
			await _fixture.AppendStream("user-w-diff", TestCredentials.TestUser2);
			await _fixture.AppendStream("user-w-diff", TestCredentials.TestAdmin);

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream("user-w-multiple"));
			await _fixture.AppendStream("user-w-multiple", TestCredentials.TestUser1);
			await _fixture.AppendStream("user-w-multiple", TestCredentials.TestUser2);
			await _fixture.AppendStream("user-w-multiple", TestCredentials.TestAdmin);

			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.AppendStream("user-w-restricted"));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.AppendStream("user-w-restricted", TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.AppendStream("user-w-restricted", TestCredentials.TestUser2));
			await _fixture.AppendStream("user-w-restricted", TestCredentials.TestAdmin);

			await _fixture.AppendStream("user-w-all");
			await _fixture.AppendStream("user-w-all", TestCredentials.TestUser1);
			await _fixture.AppendStream("user-w-all", TestCredentials.TestUser2);
			await _fixture.AppendStream("user-w-all", TestCredentials.TestAdmin);


			await _fixture.ReadEvent("user-no-acl");
			await _fixture.ReadEvent("user-no-acl", TestCredentials.TestUser1);
			await _fixture.ReadEvent("user-no-acl", TestCredentials.TestUser2);
			await _fixture.ReadEvent("user-no-acl", TestCredentials.TestAdmin);

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadEvent("user-r-restricted"));
			await _fixture.AppendStream("user-r-restricted", TestCredentials.TestUser1);
			await _fixture.ReadEvent("user-r-restricted", TestCredentials.TestUser1);
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.ReadEvent("user-r-restricted", TestCredentials.TestUser2));
			await _fixture.ReadEvent("user-r-restricted", TestCredentials.TestAdmin);
		}

		[Fact]
		public async Task acl_inheritance_is_working_properly_on_system_streams() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream("$sys-no-acl"));
			await _fixture.AppendStream("$sys-no-acl", TestCredentials.TestUser1);
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.AppendStream("$sys-no-acl", TestCredentials.TestUser2));
			await _fixture.AppendStream("$sys-no-acl", TestCredentials.TestAdmin);

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream("$sys-w-diff"));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.AppendStream("$sys-w-diff", TestCredentials.TestUser1));
			await _fixture.AppendStream("$sys-w-diff", TestCredentials.TestUser2);
			await _fixture.AppendStream("$sys-w-diff", TestCredentials.TestAdmin);

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream("$sys-w-multiple"));
			await _fixture.AppendStream("$sys-w-multiple", TestCredentials.TestUser1);
			await _fixture.AppendStream("$sys-w-multiple", TestCredentials.TestUser2);
			await _fixture.AppendStream("$sys-w-multiple", TestCredentials.TestAdmin);

			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.AppendStream("$sys-w-restricted"));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.AppendStream("$sys-w-restricted", TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.AppendStream("$sys-w-restricted", TestCredentials.TestUser2));
			await _fixture.AppendStream("$sys-w-restricted", TestCredentials.TestAdmin);

			await _fixture.AppendStream("$sys-w-all");
			await _fixture.AppendStream("$sys-w-all", TestCredentials.TestUser1);
			await _fixture.AppendStream("$sys-w-all", TestCredentials.TestUser2);
			await _fixture.AppendStream("$sys-w-all", TestCredentials.TestAdmin);

			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadEvent("$sys-no-acl"));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadEvent("$sys-no-acl", TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadEvent("$sys-no-acl", TestCredentials.TestUser2));
			await _fixture.ReadEvent("$sys-no-acl", TestCredentials.TestAdmin);
		}
	}
}
