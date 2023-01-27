using System;
using System.Threading.Tasks;
using EventStore.Client.Redaction;
using EventStore.Core.Data.Redaction;
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
			if (!await _authorizationProvider.CheckAccessAsync(user, SwitchChunkOperation, context.CancellationToken).ConfigureAwait(false))
				throw RpcExceptions.AccessDenied();

			await SwitchChunksLock().ConfigureAwait(false);
			try {
				await SwitchChunks(context, requestStream, responseStream).ConfigureAwait(false);
			} finally {
				await SwitchChunksUnlock().ConfigureAwait(false);
			}
		}

		private async Task SwitchChunksLock() {
			var lockTcs = new TaskCompletionSource<RedactionMessage.SwitchChunkLockCompleted>();
			_bus.Publish(new RedactionMessage.SwitchChunkLock(
				new CallbackEnvelope(msg => lockTcs.SetResult(msg as RedactionMessage.SwitchChunkLockCompleted))
			));

			var completionMsg = await lockTcs.Task.ConfigureAwait(false);

			// it's important not to throw any exceptions beyond this point in this method
			// (except to indicate that acquiring the lock failed), otherwise we can end up
			// in a situation where the lock remains permanently acquired.

			if (completionMsg is null) // should never happen
				return;

			var result = completionMsg.Result;
			if (result != SwitchChunkLockResult.Success)
				throw RpcExceptions.RedactionLockFailed();
		}

		private async Task SwitchChunks(
			ServerCallContext context,
			IAsyncStreamReader<SwitchChunkReq> requestStream,
			IServerStreamWriter<SwitchChunkResp> responseStream) {

			// write empty headers to indicate that acquiring the lock was successful
			await context.WriteResponseHeadersAsync(new Metadata()).ConfigureAwait(false);

			await foreach(var request in requestStream.ReadAllAsync().ConfigureAwait(false)) {
				var tcs = new TaskCompletionSource<RedactionMessage.SwitchChunkCompleted>();
				_bus.Publish(new RedactionMessage.SwitchChunk(
					new CallbackEnvelope(msg => tcs.SetResult(msg as RedactionMessage.SwitchChunkCompleted)),
					request.TargetChunkFile,
					request.NewChunkFile
				));

				var completionMsg = await tcs.Task.ConfigureAwait(false);
				if (completionMsg is null)
					throw new Exception($"Unexpected message type.");

				var result = completionMsg.Result;
				if (result != SwitchChunkResult.Success)
					throw RpcExceptions.RedactionSwitchChunkFailed(result.GetErrorMessage());

				await responseStream.WriteAsync(new SwitchChunkResp()).ConfigureAwait(false);
			}
		}

		private async Task SwitchChunksUnlock() {
			var unlockTcs = new TaskCompletionSource<RedactionMessage.SwitchChunkUnlockCompleted>();
			_bus.Publish(new RedactionMessage.SwitchChunkUnlock(
				new CallbackEnvelope(msg => unlockTcs.SetResult(msg as RedactionMessage.SwitchChunkUnlockCompleted))
			));

			var completionMsg = await unlockTcs.Task.ConfigureAwait(false);
			if (completionMsg is null)
				throw new Exception($"Unexpected message type.");

			var result = completionMsg.Result;
			if (result != SwitchChunkUnlockResult.Success)
				throw RpcExceptions.RedactionSwitchChunkFailed(result.GetErrorMessage());
		}
	}
}
