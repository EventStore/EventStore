using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class multiple_role_security : AuthenticationTestBase {
		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			var settings = new SystemSettings(
				new StreamAcl(new[] {"user1", "user2"}, new[] {"$admins", "user1"}, new[] {"user1", SystemRoles.All},
					null, null),
				null);
			Connection.SetSystemSettingsAsync(settings, new UserCredentials("adm", "admpa$$")).Wait();
		}

		[Test]
		public void multiple_roles_are_handled_correctly() {
			Expect<AccessDeniedException>(() => ReadEvent("usr-stream", null, null));
			ExpectNoException(() => ReadEvent("usr-stream", "user1", "pa$$1"));
			ExpectNoException(() => ReadEvent("usr-stream", "user2", "pa$$2"));
			ExpectNoException(() => ReadEvent("usr-stream", "adm", "admpa$$"));

			Expect<AccessDeniedException>(() => WriteStream("usr-stream", null, null));
			ExpectNoException(() => WriteStream("usr-stream", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => WriteStream("usr-stream", "user2", "pa$$2"));
			ExpectNoException(() => WriteStream("usr-stream", "adm", "admpa$$"));

			ExpectNoException(() => DeleteStream("usr-stream1", null, null));
			ExpectNoException(() => DeleteStream("usr-stream2", "user1", "pa$$1"));
			ExpectNoException(() => DeleteStream("usr-stream3", "user2", "pa$$2"));
			ExpectNoException(() => DeleteStream("usr-stream4", "adm", "admpa$$"));
		}
	}
}
