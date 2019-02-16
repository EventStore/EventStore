using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class read_all_security : AuthenticationTestBase {
		[Test]
		public void reading_all_with_not_existing_credentials_is_not_authenticated() {
			Expect<NotAuthenticatedException>(() => ReadAllForward("badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => ReadAllBackward("badlogin", "badpass"));
		}

		[Test]
		public void reading_all_with_no_credentials_is_denied() {
			Expect<AccessDeniedException>(() => ReadAllForward(null, null));
			Expect<AccessDeniedException>(() => ReadAllBackward(null, null));
		}

		[Test]
		public void reading_all_with_not_authorized_user_credentials_is_denied() {
			Expect<AccessDeniedException>(() => ReadAllForward("user2", "pa$$2"));
			Expect<AccessDeniedException>(() => ReadAllBackward("user2", "pa$$2"));
		}

		[Test]
		public void reading_all_with_authorized_user_credentials_succeeds() {
			ExpectNoException(() => ReadAllForward("user1", "pa$$1"));
			ExpectNoException(() => ReadAllBackward("user1", "pa$$1"));
		}

		[Test]
		public void reading_all_with_admin_credentials_succeeds() {
			ExpectNoException(() => ReadAllForward("adm", "admpa$$"));
			ExpectNoException(() => ReadAllBackward("adm", "admpa$$"));
		}
	}
}
