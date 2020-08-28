﻿using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class overriden_system_stream_security_for_all : AuthenticationTestBase {
		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			var settings = new SystemSettings(
				userStreamAcl: null,
				systemStreamAcl: new StreamAcl(SystemRoles.All, SystemRoles.All, SystemRoles.All, SystemRoles.All,
					SystemRoles.All));
			await Connection.SetSystemSettingsAsync(settings, new UserCredentials("adm", "admpa$$"));
		}

		[Test]
		public async Task operations_on_system_stream_succeeds_for_user() {
			const string stream = "$sys-authorized-user";
			await ReadEvent(stream, "user1", "pa$$1");
			await ReadStreamForward(stream, "user1", "pa$$1");
			await ReadStreamBackward(stream, "user1", "pa$$1");

			await WriteStream(stream, "user1", "pa$$1");
			await TransStart(stream, "user1", "pa$$1");

			var transId = (await TransStart(stream, "adm", "admpa$$")).TransactionId;
			var trans = Connection.ContinueTransaction(transId, new UserCredentials("user1", "pa$$1"));
			await trans.WriteAsync();
			await trans.CommitAsync();

			await ReadMeta(stream, "user1", "pa$$1");
			await WriteMeta(stream, "user1", "pa$$1", null);

			await SubscribeToStream(stream, "user1", "pa$$1");

			await DeleteStream(stream, "user1", "pa$$1");
		}

		[Test]
		public async Task operations_on_system_stream_fail_for_anonymous_user() {
			const string stream = "$sys-anonymous-user";
			await ReadEvent(stream, null, null);
			await ReadStreamForward(stream, null, null);
			await ReadStreamBackward(stream, null, null);

			await WriteStream(stream, null, null);
			await TransStart(stream, null, null);

			var transId = (await TransStart(stream, "adm", "admpa$$")).TransactionId;
			var trans = Connection.ContinueTransaction(transId);
			await trans.WriteAsync();
			await trans.CommitAsync();

			await ReadMeta(stream, null, null);
			await WriteMeta(stream, null, null, null);

			await SubscribeToStream(stream, null, null);

			await DeleteStream(stream, null, null);
		}

		[Test]
		public async Task operations_on_system_stream_succeed_for_admin() {
			const string stream = "$sys-admin";
			await ReadEvent(stream, "adm", "admpa$$");
			await ReadStreamForward(stream, "adm", "admpa$$");
			await ReadStreamBackward(stream, "adm", "admpa$$");

			await WriteStream(stream, "adm", "admpa$$");
			await TransStart(stream, "adm", "admpa$$");

			var transId = (await TransStart(stream, "adm", "admpa$$")).TransactionId;
			var trans = Connection.ContinueTransaction(transId, new UserCredentials("adm", "admpa$$"));
			await trans.WriteAsync();
			await trans.CommitAsync();

			await ReadMeta(stream, "adm", "admpa$$");
			await WriteMeta(stream, "adm", "admpa$$", null);

			await SubscribeToStream(stream, "adm", "admpa$$");

			await DeleteStream(stream, "adm", "admpa$$");
		}
	}
}
