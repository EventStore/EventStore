using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class subscribe_to_stream_security : AuthenticationTestBase {
		[Test]
		public void subscribing_to_stream_with_not_existing_credentials_is_not_authenticated() {
			Expect<NotAuthenticatedException>(() => SubscribeToStream("read-stream", "badlogin", "badpass"));
		}

		[Test]
		public void subscribing_to_stream_with_no_credentials_is_denied() {
			Expect<AccessDeniedException>(() => SubscribeToStream("read-stream", null, null));
		}

		[Test]
		public void subscribing_to_stream_with_not_authorized_user_credentials_is_denied() {
			Expect<AccessDeniedException>(() => SubscribeToStream("read-stream", "user2", "pa$$2"));
		}

		[Test]
		public void reading_stream_with_authorized_user_credentials_succeeds() {
			ExpectNoException(() => SubscribeToStream("read-stream", "user1", "pa$$1"));
		}

		[Test]
		public void reading_stream_with_admin_user_credentials_succeeds() {
			ExpectNoException(() => SubscribeToStream("read-stream", "adm", "admpa$$"));
		}


		[Test]
		public void subscribing_to_no_acl_stream_succeeds_when_no_credentials_are_passed() {
			ExpectNoException(() => SubscribeToStream("noacl-stream", null, null));
		}

		[Test]
		public void subscribing_to_no_acl_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			Expect<NotAuthenticatedException>(() => SubscribeToStream("noacl-stream", "badlogin", "badpass"));
		}

		[Test]
		public void subscribing_to_no_acl_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			ExpectNoException(() => SubscribeToStream("noacl-stream", "user1", "pa$$1"));
			ExpectNoException(() => SubscribeToStream("noacl-stream", "user2", "pa$$2"));
		}

		[Test]
		public void subscribing_to_no_acl_stream_succeeds_when_admin_user_credentials_are_passed() {
			ExpectNoException(() => SubscribeToStream("noacl-stream", "adm", "admpa$$"));
		}


		[Test]
		public void subscribing_to_all_access_normal_stream_succeeds_when_no_credentials_are_passed() {
			ExpectNoException(() => SubscribeToStream("normal-all", null, null));
		}

		[Test]
		public void
			subscribing_to_all_access_normal_stream_is_not_authenticated_when_not_existing_credentials_are_passed() {
			Expect<NotAuthenticatedException>(() => SubscribeToStream("normal-all", "badlogin", "badpass"));
		}

		[Test]
		public void subscribing_to_all_access_normal_stream_succeeds_when_any_existing_user_credentials_are_passed() {
			ExpectNoException(() => SubscribeToStream("normal-all", "user1", "pa$$1"));
			ExpectNoException(() => SubscribeToStream("normal-all", "user2", "pa$$2"));
		}

		[Test]
		public void subscribing_to_all_access_normal_streamm_succeeds_when_admin_user_credentials_are_passed() {
			ExpectNoException(() => SubscribeToStream("normal-all", "adm", "admpa$$"));
		}
	}
}
