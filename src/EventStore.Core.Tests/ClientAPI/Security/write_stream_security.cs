using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class write_stream_security : AuthenticationTestBase {
		[Test]
		public void writing_to_all_is_never_allowed() {
			Expect<AccessDeniedException>(() => WriteStream("$all", null, null));
			Expect<AccessDeniedException>(() => WriteStream("$all", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => WriteStream("$all", "adm", "admpa$$"));
		}

		[Test]
		public void writing_with_not_existing_credentials_is_not_authenticated() {
			Expect<NotAuthenticatedException>(() => WriteStream("write-stream", "badlogin", "badpass"));
		}

		[Test]
		public void writing_to_stream_with_no_credentials_is_denied() {
			Expect<AccessDeniedException>(() => WriteStream("write-stream", null, null));
		}

		[Test]
		public void writing_to_stream_with_not_authorized_user_credentials_is_denied() {
			Expect<AccessDeniedException>(() => WriteStream("write-stream", "user2", "pa$$2"));
		}

		[Test]
		public void writing_to_stream_with_authorized_user_credentials_succeeds() {
			ExpectNoException(() => WriteStream("write-stream", "user1", "pa$$1"));
		}

		[Test]
		public void writing_to_stream_with_admin_user_credentials_succeeds() {
			ExpectNoException(() => WriteStream("write-stream", "adm", "admpa$$"));
		}


		[Test]
		public void writing_to_no_acl_stream_succeeds_when_no_credentials_are_passed() {
			ExpectNoException(() => WriteStream("noacl-stream", null, null));
		}

		[Test]
		public void writing_to_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			Expect<NotAuthenticatedException>(() => WriteStream("noacl-stream", "badlogin", "badpass"));
		}

		[Test]
		public void writing_to_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			ExpectNoException(() => WriteStream("noacl-stream", "user1", "pa$$1"));
			ExpectNoException(() => WriteStream("noacl-stream", "user2", "pa$$2"));
		}

		[Test]
		public void writing_to_no_acl_stream_succeeds_when_any_admin_user_credentials_are_passed() {
			ExpectNoException(() => WriteStream("noacl-stream", "adm", "admpa$$"));
		}


		[Test]
		public void writing_to_all_access_normal_stream_succeeds_when_no_credentials_are_passed() {
			ExpectNoException(() => WriteStream("normal-all", null, null));
		}

		[Test]
		public void
			writing_to_all_access_normal_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			Expect<NotAuthenticatedException>(() => WriteStream("normal-all", "badlogin", "badpass"));
		}

		[Test]
		public void writing_to_all_access_normal_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			ExpectNoException(() => WriteStream("normal-all", "user1", "pa$$1"));
			ExpectNoException(() => WriteStream("normal-all", "user2", "pa$$2"));
		}

		[Test]
		public void writing_to_all_access_normal_stream_succeeds_when_any_admin_user_credentials_are_passed() {
			ExpectNoException(() => WriteStream("normal-all", "adm", "admpa$$"));
		}
	}
}
