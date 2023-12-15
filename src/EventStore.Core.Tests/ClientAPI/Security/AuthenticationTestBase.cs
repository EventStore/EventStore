extern alias GrpcClient;
extern alias GrpcClientStreams;
using System;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using GrpcClient::EventStore.Client;
using NUnit.Framework;
using ExpectedVersion = EventStore.Core.Tests.ClientAPI.Helpers.ExpectedVersion;
using StreamAcl = GrpcClientStreams::EventStore.Client.StreamAcl;
using StreamMetadata = GrpcClientStreams::EventStore.Client.StreamMetadata;
using SystemRoles = EventStore.Core.Services.SystemRoles;

namespace EventStore.Core.Tests.ClientAPI.Security {
	public abstract class AuthenticationTestBase<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		private readonly UserCredentials _userCredentials;
		private MiniNode<TLogFormat, TStreamId> _node;
		protected IEventStoreClient Connection;

		protected AuthenticationTestBase(UserCredentials userCredentials = null) {
			_userCredentials = userCredentials;
		}


		public virtual IEventStoreClient SetupConnection(MiniNode<TLogFormat, TStreamId> node) {
			return new GrpcEventStoreConnection(node.HttpEndPoint);
		}

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			_node = new MiniNode<TLogFormat, TStreamId>(PathName, enableTrustedAuth: true);
			await _node.Start();

			var userCreateEvent1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			_node.Node.MainQueue.Publish(
				new UserManagementMessage.Create(
					new CallbackEnvelope(
						m => {
							Assert.IsTrue(m is UserManagementMessage.UpdateResult);
							var msg = (UserManagementMessage.UpdateResult)m;
							Assert.IsTrue(msg.Success);

							userCreateEvent1.SetResult(true);
						}),
					SystemAccounts.System,
					"user1",
					"Test User 1",
					new string[0],
					"pa$$1"));

			var userCreateEvent2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			_node.Node.MainQueue.Publish(
				new UserManagementMessage.Create(
					new CallbackEnvelope(
						m => {
							Assert.IsTrue(m is UserManagementMessage.UpdateResult);
							var msg = (UserManagementMessage.UpdateResult)m;
							Assert.IsTrue(msg.Success);

							userCreateEvent2.SetResult(true);
						}),
					SystemAccounts.System,
					"user2",
					"Test User 2",
					new string[0],
					"pa$$2"));

			var adminCreateEvent2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			_node.Node.MainQueue.Publish(
				new UserManagementMessage.Create(
					new CallbackEnvelope(
						m => {
							Assert.IsTrue(m is UserManagementMessage.UpdateResult);
							var msg = (UserManagementMessage.UpdateResult)m;
							Assert.IsTrue(msg.Success);

							adminCreateEvent2.SetResult(true);
						}),
					SystemAccounts.System,
					"adm",
					"Administrator User",
					new[] { SystemRoles.Admins },
					"admpa$$"));

			Assert.IsTrue(await userCreateEvent1.Task.WithTimeout(10000), "User 1 creation failed");
			Assert.IsTrue(await userCreateEvent2.Task.WithTimeout(10000), "User 2 creation failed");
			Assert.IsTrue(await adminCreateEvent2.Task.WithTimeout(10000), "Administrator User creation failed");

			Connection = SetupConnection(_node);
			await Connection.ConnectAsync();

			await Connection.SetStreamMetadataAsync("noacl-stream", ExpectedVersion.NoStream, new StreamMetadata());
			await Connection.SetStreamMetadataAsync(
				"read-stream",
				ExpectedVersion.NoStream,
				new StreamMetadata(acl: new StreamAcl(readRole: "user1")));
			await Connection.SetStreamMetadataAsync(
				"write-stream",
				ExpectedVersion.NoStream,
				new StreamMetadata(acl: new StreamAcl(writeRole: "user1")));
			await Connection.SetStreamMetadataAsync(
				"metaread-stream",
				ExpectedVersion.NoStream,
				new StreamMetadata(acl: new StreamAcl(metaReadRole: "user!")));
			await Connection.SetStreamMetadataAsync(
				"metawrite-stream",
				ExpectedVersion.NoStream,
				new StreamMetadata(acl: new StreamAcl(metaWriteRole: "user1")));

			await Connection.SetStreamMetadataAsync(
				"$all",
				ExpectedVersion.Any,
				new StreamMetadata(acl: new StreamAcl(readRole: "user1")),
				new UserCredentials("adm", "admpa$$"));

			await Connection.SetStreamMetadataAsync(
				"$system-acl",
				ExpectedVersion.NoStream,
				new StreamMetadata(acl: new StreamAcl(readRole: "user1", writeRole: "user1", metaReadRole: "user1", metaWriteRole: "user1")),
				new UserCredentials("adm", "admpa$$"));
			await Connection.SetStreamMetadataAsync(
				"$system-adm",
				ExpectedVersion.NoStream,
				new StreamMetadata(acl: new StreamAcl(readRole: SystemRoles.Admins, writeRole: SystemRoles.Admins, metaReadRole: SystemRoles.Admins, metaWriteRole: SystemRoles.Admins)),
				new UserCredentials("adm", "admpa$$"));

			await Connection.SetStreamMetadataAsync(
				"normal-all",
				ExpectedVersion.NoStream,
				new StreamMetadata(acl: new StreamAcl(readRole: SystemRoles.All, writeRole: SystemRoles.All, metaReadRole: SystemRoles.All, metaWriteRole: SystemRoles.All)));
			await Connection.SetStreamMetadataAsync(
				"$system-all",
				ExpectedVersion.NoStream,
				new StreamMetadata(acl: new StreamAcl(readRole: SystemRoles.All, writeRole: SystemRoles.All, metaReadRole: SystemRoles.All, metaWriteRole: SystemRoles.All)),
				new UserCredentials("adm", "admpa$$"));
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			await _node.Shutdown();
			await Connection.Close();
			await base.TestFixtureTearDown();
		}

		protected Task ReadEvent(string streamId, string login, string password) {
			return Connection.ReadEventAsync(streamId, -1, false,
				login == null && password == null ? null : new UserCredentials(login, password));
		}

		protected Task ReadStreamForward(string streamId, string login, string password) {
			return Connection.ReadStreamEventsForwardAsync(streamId, 0, 1, false,
				login == null && password == null ? null : new UserCredentials(login, password));
		}

		protected Task ReadStreamBackward(string streamId, string login, string password) {
			return Connection.ReadStreamEventsBackwardAsync(streamId, 0, 1, false,
				login == null && password == null ? null : new UserCredentials(login, password));
		}

		protected Task WriteStream(string streamId, string login, string password) {
			return Connection.AppendToStreamAsync(streamId, ExpectedVersion.Any, CreateEvents(),
				login == null && password == null ? null : new UserCredentials(login, password));
		}

		// protected Task<EventStoreTransaction> TransStart(string streamId, string login, string password) {
		// 	return Connection.StartTransactionAsync(streamId, ExpectedVersion.Any,
		// 		login == null && password == null ? null : new UserCredentials(login, password));
		// }

		protected Task ReadAllForward(string login, string password) {
			return Connection.ReadAllEventsForwardAsync(Position.Start, 1, false,
				login == null && password == null ? null : new UserCredentials(login, password));
		}

		protected Task ReadAllBackward(string login, string password) {
			return Connection.ReadAllEventsBackwardAsync(Position.End, 1, false,
				login == null && password == null ? null : new UserCredentials(login, password));
		}

		protected Task ReadMeta(string streamId, string login, string password) {
			return Connection.GetStreamMetadataAsRawBytesAsync(streamId,
				login == null && password == null ? null : new UserCredentials(login, password));
		}

		protected Task WriteMeta(string streamId, string login, string password, string metawriteRole) {
			return Connection.SetStreamMetadataAsync(streamId, ExpectedVersion.Any,
				metawriteRole == null
					? new StreamMetadata()
					: new StreamMetadata(acl: new StreamAcl(readRole: metawriteRole, writeRole: metawriteRole, metaReadRole: metawriteRole, metaWriteRole: metawriteRole)),
				login == null && password == null ? null : new UserCredentials(login, password));
		}

		protected async Task SubscribeToStream(string streamId, string login, string password) {
			using (await Connection.SubscribeToStreamAsync(streamId, false, (x, y) => Task.CompletedTask,
				(x, y, z) => { },
				userCredentials: login == null && password == null ? null : new UserCredentials(login, password))) {
			}
		}

		protected async Task SubscribeToAll(string login, string password) {
			using (await Connection.SubscribeToAllAsync(false, (x, y) => Task.CompletedTask, (x, y, z) => { },
				login == null && password == null ? null : new UserCredentials(login, password))) {
			}
		}

		protected async Task<string> CreateStreamWithMeta(StreamMetadata metadata, string streamPrefix = null) {
			var stream = (streamPrefix ?? string.Empty) + Guid.NewGuid().ToString();
			await Connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream,
				metadata, new UserCredentials("adm", "admpa$$"));
			return stream;
		}

		protected Task DeleteStream(string streamId, string login, string password) {
			return Connection.DeleteStreamAsync(streamId, ExpectedVersion.Any, true,
				login == null && password == null ? null : new UserCredentials(login, password));
		}

		protected Task ExpectNoException(Func<Task> action) => action();

		protected EventData[] CreateEvents() {
			return new[] { new EventData(Uuid.NewUuid(), "some-type", new byte[] { 1, 2, 3 }, null) };
		}
	}
}
