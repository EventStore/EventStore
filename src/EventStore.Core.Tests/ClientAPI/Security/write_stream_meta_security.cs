using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class write_stream_meta_security : AuthenticationTestBase {
		[Test]
		public void writing_meta_with_not_existing_credentials_is_not_authenticated() {
			Expect<NotAuthenticatedException>(() => WriteMeta("metawrite-stream", "badlogin", "badpass", "user1"));
		}

		[Test]
		public void writing_meta_to_stream_with_no_credentials_is_denied() {
			Expect<AccessDeniedException>(() => WriteMeta("metawrite-stream", null, null, "user1"));
		}

		[Test]
		public void writing_meta_to_stream_with_not_authorized_user_credentials_is_denied() {
			Expect<AccessDeniedException>(() => WriteMeta("metawrite-stream", "user2", "pa$$2", "user1"));
		}

		[Test]
		public void writing_meta_to_stream_with_authorized_user_credentials_succeeds() {
			ExpectNoException(() => WriteMeta("metawrite-stream", "user1", "pa$$1", "user1"));
		}

		[Test]
		public void writing_meta_to_stream_with_admin_user_credentials_succeeds() {
			ExpectNoException(() => WriteMeta("metawrite-stream", "adm", "admpa$$", "user1"));
		}


		[Test]
		public void writing_meta_to_no_acl_stream_succeeds_when_no_credentials_are_passed() {
			ExpectNoException(() => WriteMeta("noacl-stream", null, null, null));
		}

		[Test]
		public void writing_meta_to_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			Expect<NotAuthenticatedException>(() => WriteMeta("noacl-stream", "badlogin", "badpass", null));
		}

		[Test]
		public void writing_meta_to_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			ExpectNoException(() => WriteMeta("noacl-stream", "user1", "pa$$1", null));
			ExpectNoException(() => WriteMeta("noacl-stream", "user2", "pa$$2", null));
		}

		[Test]
		public void writing_meta_to_no_acl_stream_succeeds_when_admin_user_credentials_are_passed() {
			ExpectNoException(() => WriteMeta("noacl-stream", "adm", "admpa$$", null));
		}


		[Test]
		public void writing_meta_to_all_access_normal_stream_succeeds_when_no_credentials_are_passed() {
			ExpectNoException(() => WriteMeta("normal-all", null, null, SystemRoles.All));
		}

		[Test]
		public void
			writing_meta_to_all_access_normal_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			Expect<NotAuthenticatedException>(() => WriteMeta("normal-all", "badlogin", "badpass", SystemRoles.All));
		}

		[Test]
		public void writing_meta_to_all_access_normal_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			ExpectNoException(() => WriteMeta("normal-all", "user1", "pa$$1", SystemRoles.All));
			ExpectNoException(() => WriteMeta("normal-all", "user2", "pa$$2", SystemRoles.All));
		}

		[Test]
		public void writing_meta_to_all_access_normal_stream_succeeds_when_admin_user_credentials_are_passed() {
			ExpectNoException(() => WriteMeta("normal-all", "adm", "admpa$$", SystemRoles.All));
		}
	}
}
