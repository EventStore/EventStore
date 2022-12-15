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

		public override async Task<SwitchChunkResp> SwitchChunk(SwitchChunkReq request, ServerCallContext context) {
			var user = context.GetHttpContext().User;
			if (!await _authorizationProvider.CheckAccessAsync(user, SwitchChunkOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw RpcExceptions.AccessDenied();
			}

			var tcs = new TaskCompletionSource<SwitchChunkResp>();
			_bus.Publish(new RedactionMessage.SwitchChunk(
				new CallbackEnvelope(msg => SetResult(msg, tcs)),
				request.TargetChunkFile,
				request.NewChunkFile
			));

			return await tcs.Task.ConfigureAwait(false);
		}

		private static void SetResult(Message msg, TaskCompletionSource<SwitchChunkResp> tcs) {
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
	}
}
