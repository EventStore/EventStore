using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class stream_security_inheritance : AuthenticationTestBase {
		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			var settings = new SystemSettings(userStreamAcl: new StreamAcl(null, "user1", null, null, null),
				systemStreamAcl: new StreamAcl(null, "user1", null, null, null));
			Connection.SetSystemSettingsAsync(settings, new UserCredentials("adm", "admpa$$")).Wait();

			Connection.SetStreamMetadataAsync("user-no-acl", ExpectedVersion.NoStream,
				StreamMetadata.Build(), new UserCredentials("adm", "admpa$$")).Wait();
			Connection.SetStreamMetadataAsync("user-w-diff", ExpectedVersion.NoStream,
				StreamMetadata.Build().SetWriteRole("user2"), new UserCredentials("adm", "admpa$$")).Wait();
			Connection.SetStreamMetadataAsync("user-w-multiple", ExpectedVersion.NoStream,
					StreamMetadata.Build().SetWriteRoles(new[] {"user1", "user2"}),
					new UserCredentials("adm", "admpa$$"))
				.Wait();
			Connection.SetStreamMetadataAsync("user-w-restricted", ExpectedVersion.NoStream,
				StreamMetadata.Build().SetWriteRoles(new string[0]), new UserCredentials("adm", "admpa$$")).Wait();
			Connection.SetStreamMetadataAsync("user-w-all", ExpectedVersion.NoStream,
				StreamMetadata.Build().SetWriteRole(SystemRoles.All), new UserCredentials("adm", "admpa$$")).Wait();

			Connection.SetStreamMetadataAsync("user-r-restricted", ExpectedVersion.NoStream,
				StreamMetadata.Build().SetReadRole("user1"), new UserCredentials("adm", "admpa$$")).Wait();

			Connection.SetStreamMetadataAsync("$sys-no-acl", ExpectedVersion.NoStream,
				StreamMetadata.Build(), new UserCredentials("adm", "admpa$$")).Wait();
			Connection.SetStreamMetadataAsync("$sys-w-diff", ExpectedVersion.NoStream,
				StreamMetadata.Build().SetWriteRole("user2"), new UserCredentials("adm", "admpa$$")).Wait();
			Connection.SetStreamMetadataAsync("$sys-w-multiple", ExpectedVersion.NoStream,
					StreamMetadata.Build().SetWriteRoles(new[] {"user1", "user2"}),
					new UserCredentials("adm", "admpa$$"))
				.Wait();
			Connection.SetStreamMetadataAsync("$sys-w-restricted", ExpectedVersion.NoStream,
				StreamMetadata.Build().SetWriteRoles(new string[0]), new UserCredentials("adm", "admpa$$")).Wait();
			Connection.SetStreamMetadataAsync("$sys-w-all", ExpectedVersion.NoStream,
				StreamMetadata.Build().SetWriteRole(SystemRoles.All), new UserCredentials("adm", "admpa$$")).Wait();
		}

		[Test]
		public void acl_inheritance_is_working_properly_on_user_streams() {
			Expect<AccessDeniedException>(() => WriteStream("user-no-acl", null, null));
			ExpectNoException(() => WriteStream("user-no-acl", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => WriteStream("user-no-acl", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("user-no-acl", "adm", "admpa$$"));

			Expect<AccessDeniedException>(() => WriteStream("user-w-diff", null, null));
			Expect<AccessDeniedException>(() => WriteStream("user-w-diff", "user1", "pa$$1"));
			ExpectNoException(() => WriteStream("user-w-diff", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("user-w-diff", "adm", "admpa$$"));

			Expect<AccessDeniedException>(() => WriteStream("user-w-multiple", null, null));
			ExpectNoException(() => WriteStream("user-w-multiple", "user1", "pa$$1"));
			ExpectNoException(() => WriteStream("user-w-multiple", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("user-w-multiple", "adm", "admpa$$"));

			Expect<AccessDeniedException>(() => WriteStream("user-w-restricted", null, null));
			Expect<AccessDeniedException>(() => WriteStream("user-w-restricted", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => WriteStream("user-w-restricted", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("user-w-restricted", "adm", "admpa$$"));

			ExpectNoException(() => WriteStream("user-w-all", null, null));
			ExpectNoException(() => WriteStream("user-w-all", "user1", "pa$$1"));
			ExpectNoException(() => WriteStream("user-w-all", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("user-w-all", "adm", "admpa$$"));


			ExpectNoException(() => ReadEvent("user-no-acl", null, null));
			ExpectNoException(() => ReadEvent("user-no-acl", "user1", "pa$$1"));
			ExpectNoException(() => ReadEvent("user-no-acl", "user2", "pa$$2"));
			ExpectNoException(() => ReadEvent("user-no-acl", "adm", "admpa$$"));

			Expect<AccessDeniedException>(() => ReadEvent("user-r-restricted", null, null));
			ExpectNoException(() => ReadEvent("user-r-restricted", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => ReadEvent("user-r-restricted", "user2", "pa$$2"));
			ExpectNoException(() => ReadEvent("user-r-restricted", "adm", "admpa$$"));
		}

		[Test]
		public void acl_inheritance_is_working_properly_on_system_streams() {
			Expect<AccessDeniedException>(() => WriteStream("$sys-no-acl", null, null));
			ExpectNoException(() => WriteStream("$sys-no-acl", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => WriteStream("$sys-no-acl", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("$sys-no-acl", "adm", "admpa$$"));

			Expect<AccessDeniedException>(() => WriteStream("$sys-w-diff", null, null));
			Expect<AccessDeniedException>(() => WriteStream("$sys-w-diff", "user1", "pa$$1"));
			ExpectNoException(() => WriteStream("$sys-w-diff", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("$sys-w-diff", "adm", "admpa$$"));

			Expect<AccessDeniedException>(() => WriteStream("$sys-w-multiple", null, null));
			ExpectNoException(() => WriteStream("$sys-w-multiple", "user1", "pa$$1"));
			ExpectNoException(() => WriteStream("$sys-w-multiple", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("$sys-w-multiple", "adm", "admpa$$"));

			Expect<AccessDeniedException>(() => WriteStream("$sys-w-restricted", null, null));
			Expect<AccessDeniedException>(() => WriteStream("$sys-w-restricted", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => WriteStream("$sys-w-restricted", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("$sys-w-restricted", "adm", "admpa$$"));

			ExpectNoException(() => WriteStream("$sys-w-all", null, null));
			ExpectNoException(() => WriteStream("$sys-w-all", "user1", "pa$$1"));
			ExpectNoException(() => WriteStream("$sys-w-all", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("$sys-w-all", "adm", "admpa$$"));

			Expect<AccessDeniedException>(() => ReadEvent("$sys-no-acl", null, null));
			Expect<AccessDeniedException>(() => ReadEvent("$sys-no-acl", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => ReadEvent("$sys-no-acl", "user2", "pa$$2"));
			ExpectNoException(() => ReadEvent("$sys-no-acl", "adm", "admpa$$"));
		}
	}
}
