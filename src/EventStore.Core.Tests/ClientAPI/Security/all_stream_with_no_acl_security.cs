using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class all_stream_with_no_acl_security : AuthenticationTestBase {
		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			Connection.SetStreamMetadataAsync("$all", ExpectedVersion.Any, StreamMetadata.Build(),
				new UserCredentials("adm", "admpa$$")).Wait();
		}

		[Test]
		public void write_to_all_is_never_allowed() {
			Expect<AccessDeniedException>(() => WriteStream("$all", null, null));
			Expect<AccessDeniedException>(() => WriteStream("$all", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => WriteStream("$all", "adm", "admpa$$"));
		}

		[Test]
		public void delete_of_all_is_never_allowed() {
			Expect<AccessDeniedException>(() => DeleteStream("$all", null, null));
			Expect<AccessDeniedException>(() => DeleteStream("$all", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => DeleteStream("$all", "adm", "admpa$$"));
		}


		[Test]
		public void reading_and_subscribing_is_not_allowed_when_no_credentials_are_passed() {
			Expect<AccessDeniedException>(() => ReadEvent("$all", null, null));
			Expect<AccessDeniedException>(() => ReadStreamForward("$all", null, null));
			Expect<AccessDeniedException>(() => ReadStreamBackward("$all", null, null));
			Expect<AccessDeniedException>(() => ReadMeta("$all", null, null));
			Expect<AccessDeniedException>(() => SubscribeToStream("$all", null, null));
		}

		[Test]
		public void reading_and_subscribing_is_not_allowed_for_usual_user() {
			Expect<AccessDeniedException>(() => ReadEvent("$all", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => ReadStreamForward("$all", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => ReadStreamBackward("$all", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => ReadMeta("$all", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => SubscribeToStream("$all", "user1", "pa$$1"));
		}

		[Test]
		public void reading_and_subscribing_is_allowed_for_admin_user() {
			ExpectNoException(() => ReadEvent("$all", "adm", "admpa$$"));
			ExpectNoException(() => ReadStreamForward("$all", "adm", "admpa$$"));
			ExpectNoException(() => ReadStreamBackward("$all", "adm", "admpa$$"));
			ExpectNoException(() => ReadMeta("$all", "adm", "admpa$$"));
			ExpectNoException(() => SubscribeToStream("$all", "adm", "admpa$$"));
		}


		[Test]
		public void meta_write_is_not_allowed_when_no_credentials_are_passed() {
			Expect<AccessDeniedException>(() => WriteMeta("$all", null, null, null));
		}

		[Test]
		public void meta_write_is_not_allowed_for_usual_user() {
			Expect<AccessDeniedException>(() => WriteMeta("$all", "user1", "pa$$1", null));
		}

		[Test]
		public void meta_write_is_allowed_for_admin_user() {
			ExpectNoException(() => WriteMeta("$all", "adm", "admpa$$", null));
		}
	}
}
