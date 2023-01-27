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
		private static readonly Operation ReadOperation = new(Plugins.Authorization.Operations.Streams.Read);

		public override async Task GetEventPosition(
			IAsyncStreamReader<GetEventPositionReq> requestStream,
			IServerStreamWriter<GetEventPositionResp> responseStream,
			ServerCallContext context) {

			var user = context.GetHttpContext().User;

			await foreach (var request in requestStream.ReadAllAsync().ConfigureAwait(false)) {
				var streamId = request.StreamIdentifier.StreamName.ToStringUtf8();
				var streamRevision = new StreamRevision(request.StreamRevision);

				var op = ReadOperation.WithParameter(
					Plugins.Authorization.Operations.Streams.Parameters.StreamId(streamId));

				if (!await _authorizationProvider.CheckAccessAsync(user, op, context.CancellationToken).ConfigureAwait(false))
					throw RpcExceptions.AccessDenied();

				var tcs = new TaskCompletionSource<RedactionMessage.GetEventPositionCompleted>();

				_bus.Publish(new RedactionMessage.GetEventPosition(
					new CallbackEnvelope(msg => tcs.SetResult(msg as RedactionMessage.GetEventPositionCompleted)),
					streamId,
					streamRevision.ToInt64()
				));

				var completionMsg = await tcs.Task.ConfigureAwait(false);
				if (completionMsg is null)
					throw new Exception($"Unexpected message type.");

				var result = completionMsg.Result;
				if (result != GetEventPositionResult.Success)
					throw RpcExceptions.RedactionGetEventPositionFailed(result.GetErrorMessage());

				var eventPositions = completionMsg.EventPositions;

				var response = new GetEventPositionResp();
				foreach (var eventPosition in eventPositions) {
					var pos = Position.FromInt64(eventPosition.LogPosition, eventPosition.LogPosition);
					response.EventPositions.Add(new EventStore.Client.Redaction.EventPosition {
						LogPosition = pos.PreparePosition,
						ChunkInfo = new EventStore.Client.Redaction.ChunkInfo {
							FileName = eventPosition.ChunkInfo.FileName,
							Version = eventPosition.ChunkInfo.Version,
							IsComplete = eventPosition.ChunkInfo.IsComplete,
							EventOffset = eventPosition.ChunkInfo.EventOffset
						}
					});
				}

				await responseStream.WriteAsync(response).ConfigureAwait(false);
			}
		}
	}
}
