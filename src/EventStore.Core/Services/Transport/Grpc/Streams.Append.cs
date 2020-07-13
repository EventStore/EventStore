using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client.Shared;
using EventStore.Client.Streams;
using EventStore.Core.TransactionLog.Data;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Streams {
		public override async Task<AppendResp> Append(
			IAsyncStreamReader<AppendReq> requestStream,
			ServerCallContext context) {
			if (!await requestStream.MoveNext().ConfigureAwait(false))
				throw new InvalidOperationException();

			if (requestStream.Current.ContentCase != AppendReq.ContentOneofCase.Options)
				throw new InvalidOperationException();

			var options = requestStream.Current.Options;
			var streamName = options.StreamIdentifier;
			
			var expectedVersion = options.ExpectedStreamRevisionCase switch {
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.Revision => new StreamRevision(
					options.Revision).ToInt64(),
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.Any => AnyStreamRevision.Any.ToInt64(),
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.StreamExists => AnyStreamRevision.StreamExists.ToInt64(),
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.NoStream => AnyStreamRevision.NoStream.ToInt64(),
				_ => throw new InvalidOperationException()
			};

			var requiresLeader = GetRequiresLeader(context.RequestHeaders);

			var user = context.GetHttpContext().User;
			var op = WriteOperation.WithParameter(Plugins.Authorization.Operations.Streams.Parameters.StreamId(streamName));
			if (!await _provider.CheckAccessAsync(user, op, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}

			var correlationId = Guid.NewGuid(); // TODO: JPB use request id?

			var events = new List<Event>();

			var size = 0;
			while (await requestStream.MoveNext().ConfigureAwait(false)) {
				if (requestStream.Current.ContentCase != AppendReq.ContentOneofCase.ProposedMessage)
					throw new InvalidOperationException();

				var proposedMessage = requestStream.Current.ProposedMessage;
				var data = proposedMessage.Data.ToByteArray();
				var metadata = proposedMessage.CustomMetadata.ToByteArray();

				if (!proposedMessage.Metadata.TryGetValue(Constants.Metadata.Type, out var eventType)) {
					throw RpcExceptions.RequiredMetadataPropertyMissing(Constants.Metadata.Type);
				}

				if (!proposedMessage.Metadata.TryGetValue(Constants.Metadata.ContentType, out var contentType)) {
					throw RpcExceptions.RequiredMetadataPropertyMissing(Constants.Metadata.ContentType);
				}

				size += Event.SizeOnDisk(eventType, data, metadata);

				if (size > _maxAppendSize) {
					throw RpcExceptions.MaxAppendSizeExceeded(_maxAppendSize);
				}

				events.Add(new Event(
					Uuid.FromDto(proposedMessage.Id).ToGuid(),
					eventType,
					contentType == Constants.Metadata.ContentTypes.ApplicationJson,
					data,
					metadata));
			}

			var appendResponseSource = new TaskCompletionSource<AppendResp>();

			var envelope = new CallbackEnvelope(HandleWriteEventsCompleted);

			_publisher.Publish(new ClientMessage.WriteEvents(
				correlationId,
				correlationId,
				envelope,
				requiresLeader,
				streamName,
				expectedVersion,
				events.ToArray(),
				user,
				cancellationToken: context.CancellationToken));

			return await appendResponseSource.Task.ConfigureAwait(false);

			void HandleWriteEventsCompleted(Message message) {
				if (message is ClientMessage.NotHandled notHandled && RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
					appendResponseSource.TrySetException(ex);
					return;
				}

				if (!(message is ClientMessage.WriteEventsCompleted completed)) {
					appendResponseSource.TrySetException(
						RpcExceptions.UnknownMessage<ClientMessage.WriteEventsCompleted>(message));
					return;
				}

				var response = new AppendResp();
				switch (completed.Result) {
					case OperationResult.Success:
						response.Success = new AppendResp.Types.Success();
						if (completed.LastEventNumber == -1) {
							response.Success.NoStream = new Empty();
						} else {
							response.Success.CurrentRevision = StreamRevision.FromInt64(completed.LastEventNumber);
						}

						if (completed.CommitPosition == -1) {
							response.Success.NoPosition = new Empty();
						} else {
							var position = Position.FromInt64(completed.CommitPosition, completed.PreparePosition);
							response.Success.Position = new AppendResp.Types.Position {
								CommitPosition = position.CommitPosition,
								PreparePosition = position.PreparePosition
							};
						}

						appendResponseSource.TrySetResult(response);
						return;
					case OperationResult.PrepareTimeout:
					case OperationResult.CommitTimeout:
					case OperationResult.ForwardTimeout:
						appendResponseSource.TrySetException(RpcExceptions.Timeout());
						return;
					case OperationResult.WrongExpectedVersion:
						response.WrongExpectedVersion = new AppendResp.Types.WrongExpectedVersion();

						if (completed.CurrentVersion == -1) {
							response.WrongExpectedVersion.NoStream = new Empty();
						} else {
							response.WrongExpectedVersion.CurrentRevision
								= StreamRevision.FromInt64(completed.CurrentVersion);
						}

						switch (options.ExpectedStreamRevisionCase) {
							case AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.Any:
								response.WrongExpectedVersion.Any = new Empty();
								break;
							case AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.StreamExists:
								response.WrongExpectedVersion.StreamExists = new Empty();
								break;
							default:
								response.WrongExpectedVersion.ExpectedRevision 
									= StreamRevision.FromInt64(expectedVersion);
								break;
						}
						appendResponseSource.TrySetResult(response);
						return;
					case OperationResult.StreamDeleted:
						appendResponseSource.TrySetException(RpcExceptions.StreamDeleted(streamName));
						return;
					case OperationResult.InvalidTransaction:
						appendResponseSource.TrySetException(RpcExceptions.InvalidTransaction());
						return;
					case OperationResult.AccessDenied:
						appendResponseSource.TrySetException(RpcExceptions.AccessDenied());
						return;
					default:
						appendResponseSource.TrySetException(RpcExceptions.UnknownError(completed.Result));
						return;
				}
			}
		}
	}
}
