using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Client.PersistentSubscriptions;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using Google.Protobuf;
using Grpc.Core;
using static EventStore.Client.PersistentSubscriptions.ReadReq;
using static EventStore.Core.Messages.ClientMessage.PersistentSubscriptionNackEvents;
using static EventStore.Core.Messages.ClientMessage;
using static EventStore.Client.PersistentSubscriptions.ReadReq.Types.Options;
using Action = EventStore.Client.PersistentSubscriptions.ReadReq.Types.Nack.Types.Action;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class PersistentSubscriptions {
		private static readonly Operation ProcessMessagesOperation =
			new(Plugins.Authorization.Operations.Subscriptions.ProcessMessages);

		public override Task Read(IAsyncStreamReader<ReadReq> requestStream,
			IServerStreamWriter<ReadResp> responseStream, ServerCallContext context) {
			var channel = Channel.CreateUnbounded<ReadResp>(new() {
				AllowSynchronousContinuations = false,
				SingleReader = true,
				SingleWriter = true
			});
			var correlationId = Guid.NewGuid();

			var remaining = 2;
			var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

			Send(channel.Reader, responseStream, context.CancellationToken)
				.ContinueWith(HandleCompletion, context.CancellationToken);
			Receive(channel.Writer, requestStream, correlationId, context, context.CancellationToken)
				.ContinueWith(HandleCompletion, context.CancellationToken);

			return tcs.Task.ContinueWith(task => {
				var user = context.GetHttpContext().User;

				_publisher.Publish(new UnsubscribeFromStream(Guid.NewGuid(), correlationId, new NoopEnvelope(), user));
				channel.Writer.TryComplete();

				task.Wait(context.CancellationToken);
			});

			async void HandleCompletion(Task task) {
				try {
					await task.ConfigureAwait(false);
					if (Interlocked.Decrement(ref remaining) == 0) {
						tcs.TrySetResult();
					}
				} catch (OperationCanceledException) {
					tcs.TrySetCanceled(context.CancellationToken);
				} catch (Exception ex) {
					tcs.TrySetException(ex);
				}
			}
		}

		private async Task Send(ChannelReader<ReadResp> reader, IAsyncStreamWriter<ReadResp> responseStream,
			CancellationToken cancellationToken) {
			await foreach (var response in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
				await responseStream.WriteAsync(response).ConfigureAwait(false);
			}
		}

		private async Task Receive(ChannelWriter<ReadResp> writer, IAsyncStreamReader<ReadReq> requestStream,
			Guid correlationId, ServerCallContext context, CancellationToken cancellationToken) {
			if (!await requestStream.MoveNext().ConfigureAwait(false)) {
				return;
			}

			var options = requestStream.Current.Options ??
			              throw RpcExceptions.InvalidArgument(requestStream.Current.ContentCase);

			var streamName = options.StreamOptionCase switch {
				StreamOptionOneofCase.StreamIdentifier => (string)options.StreamIdentifier,
				StreamOptionOneofCase.All => SystemStreams.AllStream,
				_ => throw RpcExceptions.InvalidArgument(options.StreamOptionCase)
			};

			var user = context.GetHttpContext().User;

			if (!await _authorizationProvider.CheckAccessAsync(user, ProcessMessagesOperation.WithParameter(
					Plugins.Authorization.Operations.Subscriptions.Parameters.StreamId(streamName)),
				cancellationToken).ConfigureAwait(false)) {
				throw RpcExceptions.AccessDenied();
			}

			var connectionName =
				context.RequestHeaders.FirstOrDefault(x => x.Key == Constants.Headers.ConnectionName)?.Value ??
				"<unknown>";
			var uuidOptionsCase = options.UuidOption.ContentCase;
			var subscriptionIdSource = new TaskCompletionSource<string>();
			var messageChannel = Channel.CreateUnbounded<Message>(new() {
				SingleReader = true,
				SingleWriter = true
			});
			var envelope = new ChannelEnvelope(messageChannel.Writer);
			
			_ = ReceiveMessages();

			_publisher.Publish(streamName switch {
				SystemStreams.AllStream => new ConnectToPersistentSubscriptionToAll(correlationId, correlationId,
					envelope, correlationId, connectionName, options.GroupName, options.BufferSize, string.Empty, user),
				_ => new ConnectToPersistentSubscriptionToStream(correlationId, correlationId, envelope, correlationId,
					connectionName, options.GroupName, streamName, options.BufferSize, string.Empty, user)
			});

			var subscriptionId = await subscriptionIdSource.Task.ConfigureAwait(false);

			while (await requestStream.MoveNext(cancellationToken).ConfigureAwait(false)) {
				_publisher.Publish(requestStream.Current.ContentCase switch {
					ContentOneofCase.Ack => new PersistentSubscriptionAckEvents(correlationId, correlationId,
						new NoopEnvelope(), subscriptionId,
						requestStream.Current.Ack.Ids.Select(id => Uuid.FromDto(id).ToGuid()).ToArray(), user),
					ContentOneofCase.Nack => new PersistentSubscriptionNackEvents(correlationId, correlationId,
						new NoopEnvelope(), subscriptionId, requestStream.Current.Nack.Reason,
						requestStream.Current.Nack.Action switch {
							Action.Unknown => NakAction.Unknown,
							Action.Park => NakAction.Park,
							Action.Retry => NakAction.Retry,
							Action.Skip => NakAction.Skip,
							Action.Stop => NakAction.Stop,
							_ => throw RpcExceptions.InvalidArgument(requestStream.Current.Nack.Action)
						},
						requestStream.Current.Nack.Ids.Select(id => Uuid.FromDto(id).ToGuid()).ToArray(), user),
					_ => throw RpcExceptions.InvalidArgument(requestStream.Current.ContentCase)
				});
			}

			Task OnMessage(Message message, CancellationToken ct) => message switch {
				SubscriptionDropped dropped => Fail(dropped.Reason switch {
					SubscriptionDropReason.AccessDenied => RpcExceptions.AccessDenied(),
					SubscriptionDropReason.NotFound or SubscriptionDropReason.PersistentSubscriptionDeleted =>
						RpcExceptions.PersistentSubscriptionDoesNotExist(streamName, options.GroupName),
					SubscriptionDropReason.SubscriberMaxCountReached => RpcExceptions
						.PersistentSubscriptionMaximumSubscribersReached(streamName, options.GroupName),
					SubscriptionDropReason.Unsubscribed => RpcExceptions.PersistentSubscriptionDropped(
						streamName, options.GroupName),
					_ => RpcExceptions.UnknownError(dropped.Reason)
				}),
				PersistentSubscriptionConfirmation confirmation => SubscriptionConfirmed(confirmation.SubscriptionId),
				PersistentSubscriptionStreamEventAppeared appeared => EventAppeared(appeared, ct),
				_ => Fail(RpcExceptions.UnknownMessage<PersistentSubscriptionConfirmation>(message))
			};

			async Task ReceiveMessages() {
				await foreach (var message in messageChannel.Reader.ReadAllAsync(cancellationToken)
					.ConfigureAwait(false)) {
					await OnMessage(message, cancellationToken).ConfigureAwait(false);
				}
			}

			Task EventAppeared(PersistentSubscriptionStreamEventAppeared appeared, CancellationToken ct) =>
				writer.WriteAsync(new() { Event = ConvertToReadEvent(appeared.Event, appeared.RetryCount) }, ct)
					.AsTask();

			Task SubscriptionConfirmed(string subscriptionId) {
				subscriptionIdSource.TrySetResult(subscriptionId);
				return writer.WriteAsync(new() {
					SubscriptionConfirmation = new() { SubscriptionId = subscriptionId }
				}, cancellationToken).AsTask();
			}

			Task Fail(Exception ex) {
				subscriptionIdSource.TrySetException(ex);
				writer.TryComplete(ex);
				return Task.CompletedTask;
			}

			ReadResp.Types.ReadEvent.Types.RecordedEvent ConvertToRecordedEvent(EventRecord e, long? commitPosition,
				long? preparePosition) {
				if (e == null) return null;
				var position = Position.FromInt64(commitPosition ?? -1, preparePosition ?? -1);
				return new() {
					Id = uuidOptionsCase switch {
						ReadReq.Types.Options.Types.UUIDOption.ContentOneofCase.String => new() {
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

			ReadResp.Types.ReadEvent ConvertToReadEvent(ResolvedEvent e, int retryCount) {
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
					readEvent.NoPosition = new();
				}

				return readEvent;
			}
		}
	}
}
