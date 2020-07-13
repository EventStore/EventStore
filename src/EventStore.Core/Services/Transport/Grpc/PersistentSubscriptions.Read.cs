using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.PersistentSubscriptions;
using EventStore.Client.Shared;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.Data;
using EventStore.Plugins.Authorization;
using Google.Protobuf;
using Grpc.Core;
using static EventStore.Core.Messages.ClientMessage.PersistentSubscriptionNackEvents;
using UUID = EventStore.Client.Shared.UUID;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class PersistentSubscriptions {
		private static readonly Operation ProcessMessagesOperation = new Operation(Plugins.Authorization.Operations.Subscriptions.ProcessMessages);
		public override async Task Read(IAsyncStreamReader<ReadReq> requestStream,
			IServerStreamWriter<ReadResp> responseStream, ServerCallContext context) {
			if (!await requestStream.MoveNext().ConfigureAwait(false)) {
				return;
			}

			if (requestStream.Current.ContentCase != ReadReq.ContentOneofCase.Options) {
				throw new InvalidOperationException();
			}

			var options = requestStream.Current.Options;
			var user = context.GetHttpContext().User;

			if (!await _authorizationProvider.CheckAccessAsync(user,
				ProcessMessagesOperation.WithParameter(Plugins.Authorization.Operations.Subscriptions.Parameters.StreamId(options.StreamIdentifier)), context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			var connectionName =
				context.RequestHeaders.FirstOrDefault(x => x.Key == Constants.Headers.ConnectionName)?.Value ??
				"<unknown>";
			var correlationId = Guid.NewGuid();
			var uuidOptionsCase = options.UuidOption.ContentCase;

			await using var enumerator = new PersistentStreamSubscriptionEnumerator(correlationId, connectionName,
				_publisher, options.StreamIdentifier, options.GroupName, options.BufferSize, user, context.CancellationToken);

			var subscriptionId = await enumerator.Started.ConfigureAwait(false);

			var read = requestStream.ForEachAsync(HandleAckNack);

			await responseStream.WriteAsync(new ReadResp {
				SubscriptionConfirmation = new ReadResp.Types.SubscriptionConfirmation {
					SubscriptionId = subscriptionId
				}
			}).ConfigureAwait(false);

			while (await enumerator.MoveNextAsync().ConfigureAwait(false)) {
				await responseStream.WriteAsync(new ReadResp {
					Event = ConvertToReadEvent(enumerator.Current)
				}).ConfigureAwait(false);
			}

			await read.ConfigureAwait(false);

			ValueTask HandleAckNack(ReadReq request) {
				_publisher.Publish(request.ContentCase switch {
					ReadReq.ContentOneofCase.Ack => new ClientMessage.PersistentSubscriptionAckEvents(
						correlationId, correlationId, new NoopEnvelope(), subscriptionId,
						request.Ack.Ids.Select(id => Uuid.FromDto(id).ToGuid()).ToArray(), user),
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
						request.Nack.Ids.Select(id => Uuid.FromDto(id).ToGuid()).ToArray(), user),
					_ => throw new InvalidOperationException()
				});

				return new ValueTask(Task.CompletedTask);
			}

			ReadResp.Types.ReadEvent.Types.RecordedEvent ConvertToRecordedEvent(EventRecord e, long? commitPosition,
				long? preparePosition) {
				if (e == null) return null;
				var position = Position.FromInt64(commitPosition ?? -1, preparePosition ?? -1);
				return new ReadResp.Types.ReadEvent.Types.RecordedEvent {
					Id = uuidOptionsCase switch {
						ReadReq.Types.Options.Types.UUIDOption.ContentOneofCase.String => new UUID {
							String = e.EventId.ToString()
						},
						_ => Uuid.FromGuid(e.EventId).ToDto()
					},
					StreamIdentifier = e.EventStreamId,
					StreamRevision = StreamRevision.FromInt64(e.EventNumber),
					CommitPosition = position.CommitPosition,
					PreparePosition = position.PreparePosition,
					Metadata = {
						[Constants.Metadata.Type] = e.EventType,
						[Constants.Metadata.Created] = e.TimeStamp.ToTicksSinceEpoch().ToString(),
						[Constants.Metadata.ContentType] = e.IsJson
							? Constants.Metadata.ContentTypes.ApplicationJson
							: Constants.Metadata.ContentTypes.ApplicationOctetStream
					},
					Data = ByteString.CopyFrom(e.Data.Span),
					CustomMetadata = ByteString.CopyFrom(e.Metadata.Span)
				};
			}

			ReadResp.Types.ReadEvent ConvertToReadEvent((ResolvedEvent, int) _) {
				var (e, retryCount) = _;
				var readEvent = new ReadResp.Types.ReadEvent {
					Link = ConvertToRecordedEvent(e.Link,
						e.OriginalPosition?.CommitPosition,
						e.OriginalPosition?.PreparePosition),
					Event = ConvertToRecordedEvent(e.Event,
						e.OriginalPosition?.CommitPosition,
						e.OriginalPosition?.PreparePosition),
					RetryCount = retryCount
				};
				if (e.OriginalPosition.HasValue) {
					var position = Position.FromInt64(
						e.OriginalPosition.Value.CommitPosition,
						e.OriginalPosition.Value.PreparePosition);
					readEvent.CommitPosition = position.CommitPosition;
				} else {
					readEvent.NoPosition = new Empty();
				}

				return readEvent;
			}
		}

		private class PersistentStreamSubscriptionEnumerator
			: IAsyncEnumerator<(ResolvedEvent resolvedEvent, int retryCount)> {
			private readonly CancellationTokenSource _disposedTokenSource;
			private readonly ConcurrentQueue<(ResolvedEvent, int, Exception)> _sendQueue;
			private readonly TaskCompletionSource<string> _subscriptionIdSource;
			private readonly CancellationTokenRegistration _tokenRegistration;

			public Task<string> Started => _subscriptionIdSource.Task;

			private (ResolvedEvent, int) _current;
			private readonly IPublisher _publisher;
			private readonly Guid _correlationId;
			private readonly ClaimsPrincipal _user;

			public (ResolvedEvent, int) Current => _current;

			public PersistentStreamSubscriptionEnumerator(Guid correlationId,
				string connectionName,
				IPublisher publisher,
				string streamName,
				string groupName,
				int bufferSize,
				ClaimsPrincipal user,
				CancellationToken cancellationToken) {
				if (connectionName == null) {
					throw new ArgumentNullException(nameof(connectionName));
				}
				if (publisher == null) {
					throw new ArgumentNullException(nameof(publisher));
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

				_correlationId = correlationId;
				_publisher = publisher;
				_disposedTokenSource = new CancellationTokenSource();
				_sendQueue = new ConcurrentQueue<(ResolvedEvent, int, Exception)>();
				_subscriptionIdSource = new TaskCompletionSource<string>();
				_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);
				_user = user;

				publisher.Publish(new ClientMessage.ConnectToPersistentSubscription(correlationId, correlationId,
					new CallbackEnvelope(OnMessage), correlationId, connectionName, groupName, streamName,
					bufferSize,
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
									Fail(RpcExceptions.PersistentSubscriptionMaximumSubscribersReached(streamName,
										groupName));
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
							Fail(RpcExceptions
								.UnknownMessage<ClientMessage.PersistentSubscriptionConfirmation>(message));
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
				_publisher.Publish(new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(), _correlationId,
					new NoopEnvelope(), _user));
				_disposedTokenSource.Dispose();
				return _tokenRegistration.DisposeAsync();
			}

			public async ValueTask<bool> MoveNextAsync() {
				(ResolvedEvent, int, Exception) _;

				while (!_sendQueue.TryDequeue(out _)) {
					try {
						await Task.Delay(1, _disposedTokenSource.Token).ConfigureAwait(false);
					} catch (ObjectDisposedException) {
						return false;
					}
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
