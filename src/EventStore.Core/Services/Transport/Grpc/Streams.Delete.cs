using System;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc;
using EventStore.Grpc.Streams;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Streams {
		public override async Task<DeleteResp> Delete(DeleteReq request, ServerCallContext context) {
			if (request.Options.DeleteOptionsCase == DeleteReq.Types.Options.DeleteOptionsOneofCase.None) {
				throw new InvalidOperationException();
			}

			var options = request.Options;
			var streamName = options.StreamName;
			var expectedVersion = options.ExpectedStreamRevisionCase switch {
				DeleteReq.Types.Options.ExpectedStreamRevisionOneofCase.Revision => new StreamRevision(
					options.Revision).ToInt64(),
				DeleteReq.Types.Options.ExpectedStreamRevisionOneofCase.AnyRevision => new AnyStreamRevision(
					options.AnyRevision).ToInt64(),
				_ => throw new InvalidOperationException()
			};

			var user = await GetUserAsync(_node, context.RequestHeaders);

			var correlationId = Guid.NewGuid(); // TODO: JPB use request id?
			var deleteResponseSource = new TaskCompletionSource<DeleteResp>();

			var envelope = new CallbackEnvelope(HandleStreamDeletedCompleted);

			_node.MainQueue.Publish(new ClientMessage.DeleteStream(
				correlationId,
				correlationId,
				envelope,
				true,
				request.Options.StreamName,
				expectedVersion,
				request.Options.DeleteOptionsCase == DeleteReq.Types.Options.DeleteOptionsOneofCase.Hard,
				user));

			return await deleteResponseSource.Task;

			void HandleStreamDeletedCompleted(Message message) {
				if (message is ClientMessage.NotHandled notHandled && RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
					deleteResponseSource.TrySetException(ex);
					return;
				}

				if (!(message is ClientMessage.DeleteStreamCompleted completed)) {
					deleteResponseSource.TrySetException(RpcExceptions.UnknownMessage(message));
					return;
				}

				switch (completed.Result) {
					case OperationResult.Success:
						var response = new DeleteResp();

						if (completed.CommitPosition == -1) {
							response.Empty = new DeleteResp.Types.Empty();
						} else {
							var position = Position.FromInt64(completed.CommitPosition, completed.PreparePosition);
							response.Position = new DeleteResp.Types.Position {
								CommitPosition = position.CommitPosition,
								PreparePosition = position.PreparePosition
							};
						}

						deleteResponseSource.TrySetResult(response);
						return;
					case OperationResult.PrepareTimeout:
					case OperationResult.CommitTimeout:
					case OperationResult.ForwardTimeout:
						deleteResponseSource.TrySetException(RpcExceptions.Timeout());
						return;
					case OperationResult.WrongExpectedVersion:
						deleteResponseSource.TrySetException(RpcExceptions.WrongExpectedVersion(streamName,
							expectedVersion));
						return;
					case OperationResult.StreamDeleted:
						deleteResponseSource.TrySetException(RpcExceptions.StreamDeleted(streamName));
						return;
					case OperationResult.InvalidTransaction:
						deleteResponseSource.TrySetException(RpcExceptions.InvalidTransaction());
						return;
					case OperationResult.AccessDenied:
						deleteResponseSource.TrySetException(RpcExceptions.AccessDenied());
						return;
					default:
						deleteResponseSource.TrySetException(RpcExceptions.UnknownError(completed.Result));
						return;
				}
			}
		}
	}
}
