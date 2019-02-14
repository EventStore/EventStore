using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class overriden_system_stream_security_for_all : AuthenticationTestBase {
		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			var settings = new SystemSettings(
				userStreamAcl: null,
				systemStreamAcl: new StreamAcl(SystemRoles.All, SystemRoles.All, SystemRoles.All, SystemRoles.All,
					SystemRoles.All));
			Connection.SetSystemSettingsAsync(settings, new UserCredentials("adm", "admpa$$")).Wait();
		}

		[Test]
		public void operations_on_system_stream_succeeds_for_user() {
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
		public void operations_on_system_stream_fail_for_anonymous_user() {
			const string stream = "$sys-anonymous-user";
			ExpectNoException(() => ReadEvent(stream, null, null));
			ExpectNoException(() => ReadStreamForward(stream, null, null));
			ExpectNoException(() => ReadStreamBackward(stream, null, null));

			ExpectNoException(() => WriteStream(stream, null, null));
			ExpectNoException(() => TransStart(stream, null, null));

			var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
			var trans = Connection.ContinueTransaction(transId);
			ExpectNoException(() => trans.WriteAsync().Wait());
			ExpectNoException(() => trans.CommitAsync().Wait());

			ExpectNoException(() => ReadMeta(stream, null, null));
			ExpectNoException(() => WriteMeta(stream, null, null, null));

			ExpectNoException(() => SubscribeToStream(stream, null, null));

			ExpectNoException(() => DeleteStream(stream, null, null));
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
