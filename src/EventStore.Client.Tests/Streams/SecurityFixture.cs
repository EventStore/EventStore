using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;
using Xunit;

namespace EventStore.Client.Streams {
	public abstract class SecurityFixture : EventStoreGrpcFixture {
		public const string NoAclStream = nameof(NoAclStream);
		public const string ReadStream = nameof(ReadStream);
		public const string WriteStream = nameof(WriteStream);
		public const string MetaReadStream = nameof(MetaReadStream);
		public const string MetaWriteStream = nameof(MetaWriteStream);
		public const string AllStream = "$all";
		public const string NormalAllStream = nameof(NormalAllStream);
		public const string SystemAllStream = "$" + nameof(SystemAllStream);
		public const string SystemAdminStream = "$" + nameof(SystemAdminStream);
		public const string SystemAclStream = "$" + nameof(SystemAclStream);

		protected override async Task Given() {
			var userCreated = new Dictionary<string, TaskCompletionSource<bool>> {
				[TestCredentials.TestUser1.Username] = new TaskCompletionSource<bool>(),
				[TestCredentials.TestUser2.Username] = new TaskCompletionSource<bool>(),
				[TestCredentials.TestAdmin.Username] = new TaskCompletionSource<bool>(),
			};

			var envelope = new CallbackEnvelope(OnUserCreated);

			Node.MainQueue.Publish(new UserManagementMessage.Create(envelope,
				SystemAccount.Principal,
				TestCredentials.TestUser1.Username,
				nameof(TestCredentials.TestUser1),
				Array.Empty<string>(),
				TestCredentials.TestUser1.Password));
			Node.MainQueue.Publish(new UserManagementMessage.Create(envelope,
				SystemAccount.Principal,
				TestCredentials.TestUser2.Username,
				nameof(TestCredentials.TestUser2),
				Array.Empty<string>(),
				TestCredentials.TestUser2.Password));
			Node.MainQueue.Publish(new UserManagementMessage.Create(envelope,
				SystemAccount.Principal,
				TestCredentials.TestAdmin.Username,
				nameof(TestCredentials.TestAdmin),
				new[] {SystemRoles.Admins},
				TestCredentials.TestAdmin.Password));

			await Task.WhenAll(userCreated.Values.Select(x => x.Task)).WithTimeout(TimeSpan.FromSeconds(10));

			await Client.SetStreamMetadataAsync(NoAclStream, AnyStreamRevision.NoStream, new StreamMetadata());
			await Client.SetStreamMetadataAsync(
				ReadStream,
				AnyStreamRevision.NoStream,
				new StreamMetadata(acl: new StreamAcl(TestCredentials.TestUser1.Username)));
			await Client.SetStreamMetadataAsync(
				WriteStream,
				AnyStreamRevision.NoStream,
				new StreamMetadata(acl: new StreamAcl(writeRole: TestCredentials.TestUser1.Username)));
			await Client.SetStreamMetadataAsync(
				MetaReadStream,
				AnyStreamRevision.NoStream,
				new StreamMetadata(acl: new StreamAcl(metaReadRole: TestCredentials.TestUser1.Username)));
			await Client.SetStreamMetadataAsync(
				MetaWriteStream,
				AnyStreamRevision.NoStream,
				new StreamMetadata(acl: new StreamAcl(metaWriteRole: TestCredentials.TestUser1.Username)));

			await Client.SetStreamMetadataAsync(
				AllStream,
				AnyStreamRevision.Any,
				new StreamMetadata(acl: new StreamAcl(readRole: TestCredentials.TestUser1.Username)),
				userCredentials: TestCredentials.TestAdmin);

			await Client.SetStreamMetadataAsync(
				SystemAclStream,
				AnyStreamRevision.NoStream,
				new StreamMetadata(acl: new StreamAcl(
					writeRole: TestCredentials.TestUser1.Username,
					readRole: TestCredentials.TestUser1.Username,
					metaWriteRole: TestCredentials.TestUser1.Username,
					metaReadRole: TestCredentials.TestUser1.Username)),
				userCredentials: TestCredentials.TestAdmin);

			await Client.SetStreamMetadataAsync(
				SystemAdminStream,
				AnyStreamRevision.NoStream,
				new StreamMetadata(acl: new StreamAcl(
					writeRole: SystemRoles.Admins,
					readRole: SystemRoles.Admins,
					metaWriteRole: SystemRoles.Admins,
					metaReadRole: SystemRoles.Admins)),
				userCredentials: TestCredentials.TestAdmin);

			await Client.SetStreamMetadataAsync(
				NormalAllStream,
				AnyStreamRevision.NoStream,
				new StreamMetadata(acl: new StreamAcl(
					writeRole: SystemRoles.All,
					readRole: SystemRoles.All,
					metaWriteRole: SystemRoles.All,
					metaReadRole: SystemRoles.All)));

			await Client.SetStreamMetadataAsync(
				SystemAllStream,
				AnyStreamRevision.NoStream,
				new StreamMetadata(acl: new StreamAcl(
					writeRole: SystemRoles.All,
					readRole: SystemRoles.All,
					metaWriteRole: SystemRoles.All,
					metaReadRole: SystemRoles.All)),
				userCredentials: TestCredentials.TestAdmin);


			void OnUserCreated(Message message) {
				Assert.True(message is UserManagementMessage.UpdateResult);

				userCreated[((UserManagementMessage.UpdateResult)message).LoginName].TrySetResult(true);
			}
		}

		public Task ReadEvent(string streamId, UserCredentials userCredentials = default) =>
			Client.ReadStreamAsync(Direction.Forwards, streamId, StreamRevision.Start, 1, resolveLinkTos: false,
					userCredentials: userCredentials)
				.ToArrayAsync()
				.AsTask();

		public Task ReadStreamForward(string streamId, UserCredentials userCredentials = default) =>
			Client.ReadStreamAsync(Direction.Forwards, streamId, StreamRevision.Start, 1, resolveLinkTos: false,
					userCredentials: userCredentials)
				.ToArrayAsync()
				.AsTask();

		public Task ReadStreamBackward(string streamId, UserCredentials userCredentials = default) =>
			Client.ReadStreamAsync(Direction.Backwards, streamId, StreamRevision.Start, 1, resolveLinkTos: false,
					userCredentials: userCredentials)
				.ToArrayAsync()
				.AsTask();

		public Task<WriteResult> AppendStream(string streamId, UserCredentials userCredentials = default) =>
			Client.AppendToStreamAsync(streamId, AnyStreamRevision.Any, CreateTestEvents(3),
				userCredentials: userCredentials);

		public Task ReadAllForward(UserCredentials userCredentials = default) =>
			Client.ReadAllAsync(Direction.Forwards, Position.Start, 1, resolveLinkTos: false,
					userCredentials: userCredentials)
				.ToArrayAsync()
				.AsTask();

		public Task ReadAllBackward(UserCredentials userCredentials = default) =>
			Client.ReadAllAsync(Direction.Backwards, Position.End, 1, resolveLinkTos: false,
					userCredentials: userCredentials)
				.ToArrayAsync()
				.AsTask();

		public Task<StreamMetadataResult> ReadMeta(string streamId, UserCredentials userCredentials = default) =>
			Client.GetStreamMetadataAsync(streamId, userCredentials: userCredentials);

		public Task<WriteResult> WriteMeta(string streamId, UserCredentials userCredentials = default,
			string role = default) =>
			Client.SetStreamMetadataAsync(streamId, AnyStreamRevision.Any,
				new StreamMetadata(acl: new StreamAcl(
					writeRole: role,
					readRole: role,
					metaWriteRole: role,
					metaReadRole: role)),
				userCredentials: userCredentials);

		public async Task SubscribeToStream(string streamId, UserCredentials userCredentials = default) {
			var source = new TaskCompletionSource<bool>();
			using (await Client.SubscribeToStreamAsync(streamId, (x, y, ct) => {
					source.TrySetResult(true);
					return Task.CompletedTask;
				},
				subscriptionDropped: (x, y, ex) => {
					if (ex == null) source.TrySetResult(true);
					else source.TrySetException(ex);
				}, userCredentials: userCredentials).WithTimeout()) {
				await source.Task.WithTimeout();
			}
		}

		public async Task SubscribeToAll(UserCredentials userCredentials = default) {
			var source = new TaskCompletionSource<bool>();
			using (await Client.SubscribeToAllAsync((x, y, ct) => {
					source.TrySetResult(true);
					return Task.CompletedTask;
				},
				subscriptionDropped: (x, y, ex) => {
					if (ex == null) source.TrySetResult(true);
					else source.TrySetException(ex);
				}, userCredentials: userCredentials).WithTimeout()) {
				await source.Task.WithTimeout();
			}
		}

		public async Task<string> CreateStreamWithMeta(StreamMetadata metadata,
			[CallerMemberName] string streamId = default) {
			await Client.SetStreamMetadataAsync(streamId, AnyStreamRevision.NoStream,
				metadata, userCredentials: TestCredentials.TestAdmin);
			return streamId;
		}

		public Task<DeleteResult> DeleteStream(string streamId, UserCredentials userCredentials = default) =>
			Client.TombstoneAsync(streamId, AnyStreamRevision.Any, userCredentials: userCredentials);
	}
}
