using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client;
using EventStore.Client.Streams;
using EventStore.Core.Settings;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Streams {
		public override async Task<AppendResp> Append(
			IAsyncStreamReader<AppendReq> requestStream,
			ServerCallContext context) {
			if (!await requestStream.MoveNext().ConfigureAwait(false))
				throw new InvalidOperationException();

			if (context.Deadline < _timeProvider.UtcNow.Add(_commitTimeout)) {
				throw new TimeoutException("Request could exceed the expected timeout.");
			}
			
			if (requestStream.Current.ContentCase != AppendReq.ContentOneofCase.Options)
				throw new InvalidOperationException();

			var options = requestStream.Current.Options;
			var streamName = options.StreamName;
			var expectedVersion = options.ExpectedStreamRevisionCase switch {
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.Revision => new StreamRevision(
					options.Revision).ToInt64(),
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.Any => AnyStreamRevision.Any.ToInt64(),
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.StreamExists => AnyStreamRevision.StreamExists.ToInt64(),
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.NoStream => AnyStreamRevision.NoStream.ToInt64(),
				_ => throw new InvalidOperationException()
			};

			var user = await GetUser(_authenticationProvider, context.RequestHeaders).ConfigureAwait(false);

			var correlationId = Guid.NewGuid(); // TODO: JPB use request id?

			var events = new List<Event>();

			var size = 0;
			while (await requestStream.MoveNext().ConfigureAwait(false)) {
				if (requestStream.Current.ContentCase != AppendReq.ContentOneofCase.ProposedMessage)
					throw new InvalidOperationException();

				var proposedMessage = requestStream.Current.ProposedMessage;
				var data = proposedMessage.Data.ToByteArray();
				size += data.Length;

				if (size > _maxAppendSize) {
					throw RpcExceptions.MaxAppendSizeExceeded(_maxAppendSize);
				}

				if (!proposedMessage.Metadata.TryGetValue(Constants.Metadata.Type, out var eventType)) {
					throw RpcExceptions.RequiredMetadataPropertyMissing(Constants.Metadata.Type);
				}

				if (!proposedMessage.Metadata.TryGetValue(Constants.Metadata.IsJson, out var isJson)) {
					throw RpcExceptions.RequiredMetadataPropertyMissing(Constants.Metadata.IsJson);
				}

				events.Add(new Event(
					Uuid.FromDto(proposedMessage.Id).ToGuid(),
					eventType,
					bool.Parse(isJson),
					data,
					proposedMessage.CustomMetadata.ToByteArray()));
			}

			var appendResponseSource = new TaskCompletionSource<AppendResp>();

			var envelope = new CallbackEnvelope(HandleWriteEventsCompleted);

			_queue.Publish(new ClientMessage.WriteEvents(
				correlationId,
				correlationId,
				envelope,
				true,
				streamName,
				expectedVersion,
				events.ToArray(),
				user));

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

				switch (completed.Result) {
					case OperationResult.Success:
						var response = new AppendResp();

						if (completed.LastEventNumber == -1) {
							response.NoStream = new AppendResp.Types.Empty();
						} else {
							response.CurrentRevision = StreamRevision.FromInt64(completed.LastEventNumber);
						}

						if (completed.CommitPosition == -1) {
							response.Empty = new AppendResp.Types.Empty();
						} else {
							var position = Position.FromInt64(completed.CommitPosition, completed.PreparePosition);
							response.Position = new AppendResp.Types.Position {
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
						appendResponseSource.TrySetException(RpcExceptions.WrongExpectedVersion(
							streamName,
							expectedVersion,
							completed.CurrentVersion));
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
