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
			if (!await _authorizationProvider.CheckAccessAsync(user, SwitchChunkOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw RpcExceptions.AccessDenied();
			}

			await SwitchChunksLock(context).ConfigureAwait(false);
			try {
				await SwitchChunks(requestStream, responseStream).ConfigureAwait(false);
			} finally {
				await SwitchChunksUnlock().ConfigureAwait(false);
			}
		}

		private async Task SwitchChunksLock(ServerCallContext context) {
			var lockTcs = new TaskCompletionSource<RedactionMessage.SwitchChunkLockCompleted>();
			_bus.Publish(new RedactionMessage.SwitchChunkLock(
				new CallbackEnvelope(msg => lockTcs.SetResult(msg as RedactionMessage.SwitchChunkLockCompleted))
			));

			var completionMsg = await lockTcs.Task.ConfigureAwait(false);
			if (completionMsg is null)
				throw new Exception($"Unexpected message type.");

			var result = completionMsg.Result;
			if (result != SwitchChunkLockResult.Success)
				throw RpcExceptions.RedactionLockFailed();

			await context.WriteResponseHeadersAsync(new Metadata()).ConfigureAwait(false);
		}

		private async Task SwitchChunks(
			IAsyncStreamReader<SwitchChunkReq> requestStream,
			IServerStreamWriter<SwitchChunkResp> responseStream) {

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
