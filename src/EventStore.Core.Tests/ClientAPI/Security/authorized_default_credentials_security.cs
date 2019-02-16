using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class authorized_default_credentials_security : AuthenticationTestBase {
		public authorized_default_credentials_security() : base(new UserCredentials("user1", "pa$$1")) {
		}

		[Test]
		public void all_operations_succeeds_when_passing_no_explicit_credentials() {
			ExpectNoException(() => ReadAllForward(null, null));
			ExpectNoException(() => ReadAllBackward(null, null));

			ExpectNoException(() => ReadEvent("read-stream", null, null));
			ExpectNoException(() => ReadStreamForward("read-stream", null, null));
			ExpectNoException(() => ReadStreamBackward("read-stream", null, null));

			ExpectNoException(() => WriteStream("write-stream", null, null));
			ExpectNoException(() => {
				var trans = TransStart("write-stream", null, null);
				trans.WriteAsync().Wait();
				trans.CommitAsync().Wait();
			});

			ExpectNoException(() => ReadMeta("metaread-stream", null, null));
			ExpectNoException(() => WriteMeta("metawrite-stream", null, null, "user1"));

			ExpectNoException(() => SubscribeToStream("read-stream", null, null));
			ExpectNoException(() => SubscribeToAll(null, null));
		}

		[Test]
		public void all_operations_are_not_authenticated_when_overriden_with_not_existing_credentials() {
			Expect<NotAuthenticatedException>(() => ReadAllForward("badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => ReadAllBackward("badlogin", "badpass"));

			Expect<NotAuthenticatedException>(() => ReadEvent("read-stream", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => ReadStreamForward("read-stream", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => ReadStreamBackward("read-stream", "badlogin", "badpass"));

			Expect<NotAuthenticatedException>(() => WriteStream("write-stream", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => TransStart("write-stream", "badlogin", "badpass"));

			var transId = TransStart("write-stream", null, null).TransactionId;
			var trans = Connection.ContinueTransaction(transId, new UserCredentials("badlogin", "badpass"));
			ExpectNoException(() => trans.WriteAsync().Wait());
			Expect<NotAuthenticatedException>(() => trans.CommitAsync().Wait());

			Expect<NotAuthenticatedException>(() => ReadMeta("metaread-stream", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => WriteMeta("metawrite-stream", "badlogin", "badpass", "user1"));

			Expect<NotAuthenticatedException>(() => SubscribeToStream("read-stream", "badlogin", "badpass"));
			Expect<NotAuthenticatedException>(() => SubscribeToAll("badlogin", "badpass"));
		}

		[Test]
		public void all_operations_are_not_authorized_when_overriden_with_not_authorized_credentials() {
			Expect<AccessDeniedException>(() => ReadAllForward("user2", "pa$$2"));
			Expect<AccessDeniedException>(() => ReadAllBackward("user2", "pa$$2"));

			Expect<AccessDeniedException>(() => ReadEvent("read-stream", "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => ReadStreamForward("read-stream", "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => ReadStreamBackward("read-stream", "user2", "pa$$2"));

			Expect<AccessDeniedException>(() => WriteStream("write-stream", "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => TransStart("write-stream", "user2", "pa$$2"));

			var transId = TransStart("write-stream", null, null).TransactionId;
			var trans = Connection.ContinueTransaction(transId, new UserCredentials("user2", "pa$$2"));
			ExpectNoException(() => trans.WriteAsync().Wait());
			Expect<AccessDeniedException>(() => trans.CommitAsync().Wait());

			Expect<AccessDeniedException>(() => ReadMeta("metaread-stream", "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => WriteMeta("metawrite-stream", "user2", "pa$$2", "user1"));

			Expect<AccessDeniedException>(() => SubscribeToStream("read-stream", "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => SubscribeToAll("user2", "pa$$2"));
		}
	}
}
