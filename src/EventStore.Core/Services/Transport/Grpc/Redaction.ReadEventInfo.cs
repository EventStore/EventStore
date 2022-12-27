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

		public override async Task ReadEventInfo(
			IAsyncStreamReader<ReadEventInfoReq> requestStream,
			IServerStreamWriter<ReadEventInfoResp> responseStream,
			ServerCallContext context) {

			var user = context.GetHttpContext().User;

			while (await requestStream.MoveNext().ConfigureAwait(false)) {
				var request = requestStream.Current;
				var streamId = request.StreamIdentifier.StreamName.ToStringUtf8();
				var streamRevision = new StreamRevision(request.StreamRevision);

				var op = ReadOperation.WithParameter(
					Plugins.Authorization.Operations.Streams.Parameters.StreamId(streamId));

				if (!await _authorizationProvider.CheckAccessAsync(user, op, context.CancellationToken).ConfigureAwait(false)) {
					throw RpcExceptions.AccessDenied();
				}

				var tcs = new TaskCompletionSource<RedactionMessage.ReadEventInfoCompleted>();

				_bus.Publish(new RedactionMessage.ReadEventInfo(
					new CallbackEnvelope(msg => tcs.SetResult(msg as RedactionMessage.ReadEventInfoCompleted)),
					streamId,
					streamRevision.ToInt64()
				));

				var completionMsg = await tcs.Task.ConfigureAwait(false);
				if (completionMsg is null)
					throw new Exception($"Unexpected message type.");

				var result = completionMsg.Result;
				if (result != ReadEventInfoResult.Success)
					throw RpcExceptions.RedactionReadEventInfoFailed(result.GetErrorMessage());

				var eventInfos = completionMsg.EventInfos;

				var response = new ReadEventInfoResp();
				foreach (var eventInfo in eventInfos) {
					var pos = Position.FromInt64(eventInfo.LogPosition, eventInfo.LogPosition);
					response.EventInfos.Add(new EventInfo {
						LogPosition = pos.PreparePosition
					});
				}

				await responseStream.WriteAsync(response).ConfigureAwait(false);
			}
		}
	}
}
