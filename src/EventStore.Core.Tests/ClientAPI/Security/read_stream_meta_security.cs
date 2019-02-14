using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class read_stream_meta_security : AuthenticationTestBase {
		[Test]
		public void reading_stream_meta_with_not_existing_credentials_is_not_authenticated() {
			Expect<NotAuthenticatedException>(() => ReadMeta("metaread-stream", "badlogin", "badpass"));
		}

		[Test]
		public void reading_stream_meta_with_no_credentials_is_denied() {
			Expect<AccessDeniedException>(() => ReadMeta("metaread-stream", null, null));
		}

		[Test]
		public void reading_stream_meta_with_not_authorized_user_credentials_is_denied() {
			Expect<AccessDeniedException>(() => ReadMeta("metaread-stream", "user2", "pa$$2"));
		}

		[Test]
		public void reading_stream_meta_with_authorized_user_credentials_succeeds() {
			ExpectNoException(() => ReadMeta("metaread-stream", "user1", "pa$$1"));
		}

		[Test]
		public void reading_stream_meta_with_admin_user_credentials_succeeds() {
			ExpectNoException(() => ReadMeta("metaread-stream", "adm", "admpa$$"));
		}


		[Test]
		public void reading_no_acl_stream_meta_succeeds_when_no_credentials_are_passed() {
			ExpectNoException(() => ReadMeta("noacl-stream", null, null));
		}

		[Test]
		public void reading_no_acl_stream_meta_is_not_authenticated_when_not_existing_credentials_are_passed() {
			Expect<NotAuthenticatedException>(() => ReadMeta("noacl-stream", "badlogin", "badpass"));
		}

		[Test]
		public void reading_no_acl_stream_meta_succeeds_when_any_existing_user_credentials_are_passed() {
			ExpectNoException(() => ReadMeta("noacl-stream", "user1", "pa$$1"));
			ExpectNoException(() => ReadMeta("noacl-stream", "user2", "pa$$2"));
		}

		[Test]
		public void reading_no_acl_stream_meta_succeeds_when_admin_user_credentials_are_passed() {
			ExpectNoException(() => ReadMeta("noacl-stream", "adm", "admpa$$"));
		}


		[Test]
		public void reading_all_access_normal_stream_meta_succeeds_when_no_credentials_are_passed() {
			ExpectNoException(() => ReadMeta("normal-all", null, null));
		}

		[Test]
		public void
			reading_all_access_normal_stream_meta_is_not_authenticated_when_not_existing_credentials_are_passed() {
			Expect<NotAuthenticatedException>(() => ReadMeta("normal-all", "badlogin", "badpass"));
		}

		[Test]
		public void reading_all_access_normal_stream_meta_succeeds_when_any_existing_user_credentials_are_passed() {
			ExpectNoException(() => ReadMeta("normal-all", "user1", "pa$$1"));
			ExpectNoException(() => ReadMeta("normal-all", "user2", "pa$$2"));
		}

		[Test]
		public void reading_all_access_normal_stream_meta_succeeds_when_admin_user_credentials_are_passed() {
			ExpectNoException(() => ReadMeta("normal-all", "adm", "admpa$$"));
		}
	}
}
