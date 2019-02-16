using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class overriden_system_stream_security : AuthenticationTestBase {
		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			var settings = new SystemSettings(userStreamAcl: null,
				systemStreamAcl: new StreamAcl("user1", "user1", "user1", "user1", "user1"));
			Connection.SetSystemSettingsAsync(settings, new UserCredentials("adm", "admpa$$")).Wait();
		}

		[Test]
		public void operations_on_system_stream_succeed_for_authorized_user() {
			const string stream = "$sys-authorized-user";
			ExpectNoException(() => ReadEvent(stream, "user1", "pa$$1"));
			ExpectNoException(() => ReadStreamForward(stream, "user1", "pa$$1"));
			ExpectNoException(() => ReadStreamBackward(stream, "user1", "pa$$1"));

			ExpectNoException(() => WriteStream(stream, "user1", "pa$$1"));
			ExpectNoException(() => TransStart(stream, "user1", "pa$$1"));

			var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
			var trans = Connection.ContinueTransaction(transId, new UserCredentials("user1", "pa$$1"));
			ExpectNoException(() => trans.WriteAsync().Wait());
			ExpectNoException(() => trans.CommitAsync().Wait());

			ExpectNoException(() => ReadMeta(stream, "user1", "pa$$1"));
			ExpectNoException(() => WriteMeta(stream, "user1", "pa$$1", null));

			ExpectNoException(() => SubscribeToStream(stream, "user1", "pa$$1"));

			ExpectNoException(() => DeleteStream(stream, "user1", "pa$$1"));
		}

		[Test]
		public void operations_on_system_stream_fail_for_not_authorized_user() {
			const string stream = "$sys-not-authorized-user";
			Expect<AccessDeniedException>(() => ReadEvent(stream, "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => ReadStreamForward(stream, "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => ReadStreamBackward(stream, "user2", "pa$$2"));

			Expect<AccessDeniedException>(() => WriteStream(stream, "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => TransStart(stream, "user2", "pa$$2"));

			var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
			var trans = Connection.ContinueTransaction(transId, new UserCredentials("user2", "pa$$2"));
			ExpectNoException(() => trans.WriteAsync().Wait());
			Expect<AccessDeniedException>(() => trans.CommitAsync().Wait());

			Expect<AccessDeniedException>(() => ReadMeta(stream, "user2", "pa$$2"));
			Expect<AccessDeniedException>(() => WriteMeta(stream, "user2", "pa$$2", null));

			Expect<AccessDeniedException>(() => SubscribeToStream(stream, "user2", "pa$$2"));

			Expect<AccessDeniedException>(() => DeleteStream(stream, "user2", "pa$$2"));
		}

		[Test]
		public void operations_on_system_stream_fail_for_anonymous_user() {
			const string stream = "$sys-anonymous-user";
			Expect<AccessDeniedException>(() => ReadEvent(stream, null, null));
			Expect<AccessDeniedException>(() => ReadStreamForward(stream, null, null));
			Expect<AccessDeniedException>(() => ReadStreamBackward(stream, null, null));

			Expect<AccessDeniedException>(() => WriteStream(stream, null, null));
			Expect<AccessDeniedException>(() => TransStart(stream, null, null));

			var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
			var trans = Connection.ContinueTransaction(transId);
			ExpectNoException(() => trans.WriteAsync().Wait());
			Expect<AccessDeniedException>(() => trans.CommitAsync().Wait());

			Expect<AccessDeniedException>(() => ReadMeta(stream, null, null));
			Expect<AccessDeniedException>(() => WriteMeta(stream, null, null, null));

			Expect<AccessDeniedException>(() => SubscribeToStream(stream, null, null));

			Expect<AccessDeniedException>(() => DeleteStream(stream, null, null));
		}

		[Test]
		public void operations_on_system_stream_succeed_for_admin() {
			const string stream = "$sys-admin";
			ExpectNoException(() => ReadEvent(stream, "adm", "admpa$$"));
			ExpectNoException(() => ReadStreamForward(stream, "adm", "admpa$$"));
			ExpectNoException(() => ReadStreamBackward(stream, "adm", "admpa$$"));

			ExpectNoException(() => WriteStream(stream, "adm", "admpa$$"));
			ExpectNoException(() => TransStart(stream, "adm", "admpa$$"));

			var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
			var trans = Connection.ContinueTransaction(transId, new UserCredentials("adm", "admpa$$"));
			ExpectNoException(() => trans.WriteAsync().Wait());
			ExpectNoException(() => trans.CommitAsync().Wait());

			ExpectNoException(() => ReadMeta(stream, "adm", "admpa$$"));
			ExpectNoException(() => WriteMeta(stream, "adm", "admpa$$", null));

			ExpectNoException(() => SubscribeToStream(stream, "adm", "admpa$$"));

			ExpectNoException(() => DeleteStream(stream, "adm", "admpa$$"));
		}
	}
}
