using System;
using System.Threading.Tasks;
using EventStore.Client.Redaction;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class Redaction {
		private static readonly Operation SwitchChunkOperation = new(EventStore.Plugins.Authorization.Operations.Node.Redaction.SwitchChunk);

		public override async Task SwitchChunk(
			IAsyncStreamReader<SwitchChunkReq> requestStream,
			IServerStreamWriter<SwitchChunkResp> responseStream,
			ServerCallContext context) {

			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, SwitchChunkOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw RpcExceptions.AccessDenied();
			}

			await SwitchChunksLock().ConfigureAwait(false);
			await SwitchChunks(requestStream, responseStream).ConfigureAwait(false);
			await SwitchChunksUnlock().ConfigureAwait(false);
		}

		private async Task SwitchChunksLock() {
			var lockTcs = new TaskCompletionSource<Message>();
			_bus.Publish(new RedactionMessage.SwitchChunkLock(
				new CallbackEnvelope(msg => lockTcs.SetResult(msg))
			));

			var lockResult = await lockTcs.Task.ConfigureAwait(false);
			switch (lockResult) {
				case RedactionMessage.SwitchChunkLockSucceeded:
					break;
				case RedactionMessage.SwitchChunkLockFailed:
					throw RpcExceptions.RedactionSwitchChunkFailed("Failed to acquire lock.");
				default:
					throw new Exception($"Unexpected message type: {lockResult.GetType()}");
			}
		}

		private async Task SwitchChunks(
			IAsyncStreamReader<SwitchChunkReq> requestStream,
			IServerStreamWriter<SwitchChunkResp> responseStream) {

			await foreach(var request in requestStream.ReadAllAsync().ConfigureAwait(false)) {
				var tcs = new TaskCompletionSource<SwitchChunkResp>();
				_bus.Publish(new RedactionMessage.SwitchChunk(
					new CallbackEnvelope(msg => SetSwitchChunkResult(msg, tcs)),
					request.TargetChunkFile,
					request.NewChunkFile
				));
				var response = await tcs.Task.ConfigureAwait(false);
				await responseStream.WriteAsync(response).ConfigureAwait(false);
			}
		}

		private static void SetSwitchChunkResult(Message msg, TaskCompletionSource<SwitchChunkResp> tcs) {
			switch (msg)
			{
				case RedactionMessage.SwitchChunkSucceeded:
					tcs.SetResult(new SwitchChunkResp());
					break;
				case RedactionMessage.SwitchChunkFailed failMsg:
					tcs.SetException(RpcExceptions.RedactionSwitchChunkFailed(failMsg.Reason));
					break;
				default:
					tcs.SetException(new Exception($"Unexpected message type: {msg.GetType()}"));
					break;
			}
		}

		private async Task SwitchChunksUnlock() {
			var unlockTcs = new TaskCompletionSource<Message>();
			_bus.Publish(new RedactionMessage.SwitchChunkUnlock(
				new CallbackEnvelope(msg => unlockTcs.SetResult(msg))
			));

			var unlockResult = await unlockTcs.Task.ConfigureAwait(false);
			switch (unlockResult) {
				case RedactionMessage.SwitchChunkUnlockSucceeded:
					break;
				case RedactionMessage.SwitchChunkUnlockFailed:
					throw RpcExceptions.RedactionSwitchChunkFailed("Failed to release lock.");
				default:
					throw new Exception($"Unexpected message type: {unlockResult.GetType()}");
			}
		}
	}
}
