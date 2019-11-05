using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc;
using EventStore.Grpc.PersistentSubscriptions;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Utils;
using static EventStore.Core.Messages.ClientMessage.PersistentSubscriptionNackEvents;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class PersistentSubscriptions {
		public override async Task Read(IAsyncStreamReader<ReadReq> requestStream,
			IServerStreamWriter<ReadResp> responseStream, ServerCallContext context) {
			if (!await requestStream.MoveNext()) {
				return;
			}

			if (requestStream.Current.ContentCase != ReadReq.ContentOneofCase.Options) {
				throw new InvalidOperationException();
			}

			var options = requestStream.Current.Options;
			var user = await GetUser(_authenticationProvider, context.RequestHeaders);
			var correlationId = Guid.NewGuid();
			var source = new TaskCompletionSource<bool>();
			string subscriptionId = default;
			context.CancellationToken.Register(source.SetCanceled);

#pragma warning disable 4014
			Task.Run(() => requestStream.ForEachAsync(HandleAckNack));
#pragma warning restore 4014

			await using var enumerator = new PersistentStreamSubscriptionEnumerator(correlationId, _queue,
				options.StreamName, options.GroupName, options.BufferSize, user, context.CancellationToken);

			subscriptionId = await enumerator.Started;

			await responseStream.WriteAsync(new ReadResp {
				Empty = new ReadResp.Types.Empty()
			});

			while (await enumerator.MoveNextAsync()) {
				await responseStream.WriteAsync(new ReadResp {
					Event = ConvertToReadEvent(enumerator.Current)
				});
			}

			Task HandleAckNack(ReadReq request) {
				_queue.Publish(request.ContentCase switch {
					ReadReq.ContentOneofCase.Ack => (Message)
					new ClientMessage.PersistentSubscriptionAckEvents(
						correlationId, correlationId, new NoopEnvelope(), subscriptionId,
						request.Ack.Ids.Select(id => new Uuid(id.Span).ToGuid()).ToArray(), user),
					ReadReq.ContentOneofCase.Nack =>
					new ClientMessage.PersistentSubscriptionNackEvents(
						correlationId, correlationId, new NoopEnvelope(), subscriptionId,
						request.Nack.Reason, request.Nack.Action switch {
							ReadReq.Types.Nack.Types.Action.Unknown => NakAction.Unknown,
							ReadReq.Types.Nack.Types.Action.Park => NakAction.Park,
							ReadReq.Types.Nack.Types.Action.Retry => NakAction.Retry,
							ReadReq.Types.Nack.Types.Action.Skip => NakAction.Skip,
							ReadReq.Types.Nack.Types.Action.Stop => NakAction.Stop,
							_ => throw new InvalidOperationException()
						},
						request.Nack.Ids.Select(id => new Uuid(id.Span).ToGuid()).ToArray(), user),
					_ => throw new InvalidOperationException()
				});

				return Task.CompletedTask;
			}

			ReadResp.Types.ReadEvent.Types.RecordedEvent ConvertToRecordedEvent(EventRecord e, long? commitPosition) {
				if (e == null) return null;
				var position = Position.FromInt64(commitPosition ?? e.LogPosition, e.TransactionPosition);
				return new ReadResp.Types.ReadEvent.Types.RecordedEvent {
					Id = ByteString.CopyFrom(Uuid.FromGuid(e.EventId).ToSpan()),
					StreamName = e.EventStreamId,
					StreamRevision = StreamRevision.FromInt64(e.EventNumber),
					CommitPosition = position.CommitPosition,
					PreparePosition = position.PreparePosition,
					Metadata = {
						[Constants.Metadata.Type] = e.EventType,
						[Constants.Metadata.IsJson] = e.IsJson.ToString(),
						[Constants.Metadata.Created] = e.TimeStamp.ToBinary().ToString()
					},
					Data = ByteString.CopyFrom(e.Data),
					CustomMetadata = ByteString.CopyFrom(e.Metadata)
				};
			}

			ReadResp.Types.ReadEvent ConvertToReadEvent((ResolvedEvent, int) _) {
				var (e, retryCount) = _;
				var readEvent = new ReadResp.Types.ReadEvent {
					Link = ConvertToRecordedEvent(e.Link, e.OriginalPosition?.CommitPosition),
					Event = ConvertToRecordedEvent(e.Event, e.OriginalPosition?.CommitPosition),
					RetryCount = retryCount
				};
				if (e.OriginalPosition.HasValue) {
					var position = Position.FromInt64(
						e.OriginalPosition.Value.CommitPosition,
						e.OriginalPosition.Value.PreparePosition);
					readEvent.CommitPosition = position.CommitPosition;
				} else {
					readEvent.NoPosition = new ReadResp.Types.Empty();
				}

				return readEvent;
			}
		}

		private class PersistentStreamSubscriptionEnumerator
			: IAsyncEnumerator<(ResolvedEvent resolvedEvent, int retryCount)> {
			private readonly CancellationTokenSource _disposedTokenSource;
			private readonly ConcurrentQueue<(ResolvedEvent, int, Exception)> _sendQueue;
			private readonly TaskCompletionSource<string> _subscriptionIdSource;

			public Task<string> Started => _subscriptionIdSource.Task;

			private (ResolvedEvent, int) _current;

			public (ResolvedEvent, int) Current => _current;

			public PersistentStreamSubscriptionEnumerator(
				Guid correlationId,
				IPublisher queue,
				string streamName,
				string groupName,
				int bufferSize,
				IPrincipal user,
				CancellationToken cancellationToken) {
				if (queue == null) {
					throw new ArgumentNullException(nameof(queue));
				}

				if (streamName == null) {
					throw new ArgumentNullException(nameof(streamName));
				}

				if (groupName == null) {
					throw new ArgumentNullException(nameof(groupName));
				}

				if (correlationId == Guid.Empty) {
					throw new ArgumentException($"{nameof(correlationId)} should be non empty.", nameof(correlationId));
				}

				_disposedTokenSource = new CancellationTokenSource();
				_sendQueue = new ConcurrentQueue<(ResolvedEvent, int, Exception)>();
				_subscriptionIdSource = new TaskCompletionSource<string>();
				cancellationToken.Register(_disposedTokenSource.Dispose);

				queue.Publish(new ClientMessage.ConnectToPersistentSubscription(correlationId, correlationId,
					new CallbackEnvelope(OnMessage), correlationId, groupName, streamName, bufferSize,
					string.Empty, user));

				void OnMessage(Message message) {
					switch (message) {
						case ClientMessage.SubscriptionDropped dropped:
							switch (dropped.Reason) {
								case SubscriptionDropReason.AccessDenied:
									Fail(RpcExceptions.AccessDenied());
									return;
								case SubscriptionDropReason.NotFound:
								case SubscriptionDropReason.PersistentSubscriptionDeleted:
									Fail(RpcExceptions.PersistentSubscriptionDoesNotExist(streamName, groupName));
									return;
								case SubscriptionDropReason.SubscriberMaxCountReached:
									Fail(RpcExceptions.PersistentSubscriptionMaximumSubscribersReached(streamName, groupName));
									return;
								case SubscriptionDropReason.Unsubscribed:
									Fail(RpcExceptions.PersistentSubscriptionDropped(streamName, groupName));
									return;
								default:
									Fail(RpcExceptions.UnknownError(dropped.Reason));
									return;
							}
						case ClientMessage.PersistentSubscriptionConfirmation confirmation:
							_subscriptionIdSource.TrySetResult(confirmation.SubscriptionId);
							return;
						case ClientMessage.PersistentSubscriptionStreamEventAppeared appeared:
							EnqueueEvent(appeared.Event, appeared.RetryCount);
							return;
						default:
							Fail(RpcExceptions.UnknownMessage<ClientMessage.PersistentSubscriptionConfirmation>(message));
							return;
					}
				}

				void Fail(Exception ex) {
					_sendQueue.Enqueue((default, default, ex));
					_subscriptionIdSource.TrySetException(ex);
				}

				void EnqueueEvent(ResolvedEvent resolvedEvent, int retryCount) =>
					_sendQueue.Enqueue((resolvedEvent, retryCount, null));
			}

			public ValueTask DisposeAsync() {
				_disposedTokenSource.Dispose();
				return default;
			}

			public async ValueTask<bool> MoveNextAsync() {
				(ResolvedEvent, int, Exception) _;

				while (!_sendQueue.TryDequeue(out _)) {
					await Task.Delay(1, _disposedTokenSource.Token);
				}

				var (resolvedEvent, retryCount, exception) = _;

				if (exception != null) {
					throw exception;
				}

				_current = (resolvedEvent, retryCount);

				return true;
			}
		}
	}
}
