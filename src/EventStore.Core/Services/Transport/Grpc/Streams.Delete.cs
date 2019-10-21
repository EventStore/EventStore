using System;
using System.Security.Principal;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc;
using EventStore.Grpc.Streams;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Streams {
		public override async Task<DeleteResp> Delete(DeleteReq request, ServerCallContext context) {
			var options = request.Options;
			var streamName = options.StreamName;
			var expectedVersion = options.ExpectedStreamRevisionCase switch {
				DeleteReq.Types.Options.ExpectedStreamRevisionOneofCase.Revision =>
				new StreamRevision(options.Revision).ToInt64(),
				DeleteReq.Types.Options.ExpectedStreamRevisionOneofCase.Any =>
				AnyStreamRevision.Any.ToInt64(),
				DeleteReq.Types.Options.ExpectedStreamRevisionOneofCase.NoStream =>
				AnyStreamRevision.NoStream.ToInt64(),
				DeleteReq.Types.Options.ExpectedStreamRevisionOneofCase.StreamExists =>
				AnyStreamRevision.StreamExists.ToInt64(),
				_ => throw new InvalidOperationException()
			};

			var user = await GetUserAsync(_node, context.RequestHeaders);

			var position = await DeleteInternal(streamName, expectedVersion, user, false);

			return position.HasValue
				? new DeleteResp {
					Position = new DeleteResp.Types.Position {
						CommitPosition = position.Value.CommitPosition,
						PreparePosition = position.Value.PreparePosition
					}
				}
				: new DeleteResp {
					Empty = new DeleteResp.Types.Empty()
				};
		}

		public override async Task<TombstoneResp> Tombstone(TombstoneReq request, ServerCallContext context) {
			var options = request.Options;
			var streamName = options.StreamName;
			var expectedVersion = options.ExpectedStreamRevisionCase switch {
				TombstoneReq.Types.Options.ExpectedStreamRevisionOneofCase.Revision =>
				new StreamRevision(options.Revision).ToInt64(),
				TombstoneReq.Types.Options.ExpectedStreamRevisionOneofCase.Any =>
				AnyStreamRevision.Any.ToInt64(),
				TombstoneReq.Types.Options.ExpectedStreamRevisionOneofCase.NoStream =>
				AnyStreamRevision.NoStream.ToInt64(),
				TombstoneReq.Types.Options.ExpectedStreamRevisionOneofCase.StreamExists =>
				AnyStreamRevision.StreamExists.ToInt64(),
				_ => throw new InvalidOperationException()
			};

			var user = await GetUserAsync(_node, context.RequestHeaders);

			var position = await DeleteInternal(streamName, expectedVersion, user, true);

			return position.HasValue
				? new TombstoneResp {
					Position = new TombstoneResp.Types.Position {
						CommitPosition = position.Value.CommitPosition,
						PreparePosition = position.Value.PreparePosition
					}
				}
				: new TombstoneResp {
					Empty = new TombstoneResp.Types.Empty()
				};
		}

		private async Task<Position?> DeleteInternal(string streamName, long expectedVersion,
			IPrincipal user, bool hardDelete) {
			var correlationId = Guid.NewGuid(); // TODO: JPB use request id?
			var deleteResponseSource = new TaskCompletionSource<Position?>();

			var envelope = new CallbackEnvelope(HandleStreamDeletedCompleted);

			_node.MainQueue.Publish(new ClientMessage.DeleteStream(
				correlationId,
				correlationId,
				envelope,
				true,
				streamName,
				expectedVersion,
				hardDelete,
				user));

			return await deleteResponseSource.Task;

			void HandleStreamDeletedCompleted(Message message) {
				if (message is ClientMessage.NotHandled notHandled &&
				    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
					deleteResponseSource.TrySetException(ex);
					return;
				}

				if (!(message is ClientMessage.DeleteStreamCompleted completed)) {
					deleteResponseSource.TrySetException(
						RpcExceptions.UnknownMessage<ClientMessage.DeleteStreamCompleted>(message));
					return;
				}

				switch (completed.Result) {
					case OperationResult.Success:
						deleteResponseSource.TrySetResult(completed.CommitPosition == -1
							? default
							: Position.FromInt64(completed.CommitPosition, completed.PreparePosition));

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
