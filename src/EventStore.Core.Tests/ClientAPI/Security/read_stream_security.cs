using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class read_stream_security : AuthenticationTestBase {
		[Test]
		public void reading_stream_with_not_existing_credentials_is_not_authenticated() {
			Expect<NotAuthenticatedException>(() => ReadEvent("read-stream", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => ReadStreamForward("read-stream", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => ReadStreamBackward("read-stream", "badlogin", "badpass"));
		}

		[Test]
		public void reading_stream_with_no_credentials_is_denied() {
			Expect<AccessDeniedException>(() => ReadEvent("read-stream", null, null));
			Expect<AccessDeniedException>(() => ReadStreamForward("read-stream", null, null));
			Expect<AccessDeniedException>(() => ReadStreamBackward("read-stream", null, null));
		}

		[Test]
		public void reading_stream_with_not_authorized_user_credentials_is_denied() {
			Expect<AccessDeniedException>(() => ReadEvent("read-stream", "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => ReadStreamForward("read-stream", "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => ReadStreamBackward("read-stream", "user2", "pa$$2"));
		}

		[Test]
		public void reading_stream_with_authorized_user_credentials_succeeds() {
			ExpectNoException(() => ReadEvent("read-stream", "user1", "pa$$1"));
			ExpectNoException(() => ReadStreamForward("read-stream", "user1", "pa$$1"));
			ExpectNoException(() => ReadStreamBackward("read-stream", "user1", "pa$$1"));
		}

		[Test]
		public void reading_stream_with_admin_user_credentials_succeeds() {
			ExpectNoException(() => ReadEvent("read-stream", "adm", "admpa$$"));
			ExpectNoException(() => ReadStreamForward("read-stream", "adm", "admpa$$"));
			ExpectNoException(() => ReadStreamBackward("read-stream", "adm", "admpa$$"));
		}


		[Test]
		public void reading_no_acl_stream_succeeds_when_no_credentials_are_passed() {
			ExpectNoException(() => ReadEvent("noacl-stream", null, null));
			ExpectNoException(() => ReadStreamForward("noacl-stream", null, null));
			ExpectNoException(() => ReadStreamBackward("noacl-stream", null, null));
		}

		[Test]
		public void reading_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			Expect<NotAuthenticatedException>(() => ReadEvent("noacl-stream", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => ReadStreamForward("noacl-stream", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => ReadStreamBackward("noacl-stream", "badlogin", "badpass"));
		}

		[Test]
		public void reading_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			ExpectNoException(() => ReadEvent("noacl-stream", "user1", "pa$$1"));
			ExpectNoException(() => ReadStreamForward("noacl-stream", "user1", "pa$$1"));
			ExpectNoException(() => ReadStreamBackward("noacl-stream", "user1", "pa$$1"));
			ExpectNoException(() => ReadEvent("noacl-stream", "user2", "pa$$2"));
			ExpectNoException(() => ReadStreamForward("noacl-stream", "user2", "pa$$2"));
			ExpectNoException(() => ReadStreamBackward("noacl-stream", "user2", "pa$$2"));
		}

		[Test]
		public void reading_no_acl_stream_succeeds_when_admin_user_credentials_are_passed() {
			ExpectNoException(() => ReadEvent("noacl-stream", "adm", "admpa$$"));
			ExpectNoException(() => ReadStreamForward("noacl-stream", "adm", "admpa$$"));
			ExpectNoException(() => ReadStreamBackward("noacl-stream", "adm", "admpa$$"));
		}


		[Test]
		public void reading_all_access_normal_stream_succeeds_when_no_credentials_are_passed() {
			ExpectNoException(() => ReadEvent("normal-all", null, null));
			ExpectNoException(() => ReadStreamForward("normal-all", null, null));
			ExpectNoException(() => ReadStreamBackward("normal-all", null, null));
		}

		[Test]
		public void reading_all_access_normal_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			Expect<NotAuthenticatedException>(() => ReadEvent("normal-all", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => ReadStreamForward("normal-all", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => ReadStreamBackward("normal-all", "badlogin", "badpass"));
		}

		[Test]
		public void reading_all_access_normal_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			ExpectNoException(() => ReadEvent("normal-all", "user1", "pa$$1"));
			ExpectNoException(() => ReadStreamForward("normal-all", "user1", "pa$$1"));
			ExpectNoException(() => ReadStreamBackward("normal-all", "user1", "pa$$1"));
			ExpectNoException(() => ReadEvent("normal-all", "user2", "pa$$2"));
			ExpectNoException(() => ReadStreamForward("normal-all", "user2", "pa$$2"));
			ExpectNoException(() => ReadStreamBackward("normal-all", "user2", "pa$$2"));
		}

		[Test]
		public void reading_all_access_normal_stream_succeeds_when_admin_user_credentials_are_passed() {
			ExpectNoException(() => ReadEvent("normal-all", "adm", "admpa$$"));
			ExpectNoException(() => ReadStreamForward("normal-all", "adm", "admpa$$"));
			ExpectNoException(() => ReadStreamBackward("normal-all", "adm", "admpa$$"));
		}
	}
}
