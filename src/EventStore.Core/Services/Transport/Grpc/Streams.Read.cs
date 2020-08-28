using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Util;
using EventStore.Client;
using EventStore.Client.Shared;
using EventStore.Client.Streams;
using EventStore.Core.Exceptions;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.Services;
using Google.Protobuf;
using Grpc.Core;
using CountOptionOneofCase = EventStore.Client.Streams.ReadReq.Types.Options.CountOptionOneofCase;
using FilterOptionOneofCase = EventStore.Client.Streams.ReadReq.Types.Options.FilterOptionOneofCase;
using ReadDirection = EventStore.Client.Streams.ReadReq.Types.Options.Types.ReadDirection;
using StreamOptionOneofCase = EventStore.Client.Streams.ReadReq.Types.Options.StreamOptionOneofCase;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Streams {
		public override async Task Read(
			ReadReq request,
			IServerStreamWriter<ReadResp> responseStream,
			ServerCallContext context) {
			var options = request.Options;
			var countOptionsCase = options.CountOptionCase;
			var streamOptionsCase = options.StreamOptionCase;
			var readDirection = options.ReadDirection;
			var filterOptionsCase = options.FilterOptionCase;
			var uuidOptionsCase = options.UuidOption.ContentCase;

			var user = context.GetHttpContext().User;
			var requiresLeader = GetRequiresLeader(context.RequestHeaders);

			var op = streamOptionsCase switch {
				StreamOptionOneofCase.Stream => ReadOperation.WithParameter(
					Plugins.Authorization.Operations.Streams.Parameters.StreamId(request.Options.Stream.StreamIdentifier)),
				StreamOptionOneofCase.All => ReadOperation.WithParameter(
					Plugins.Authorization.Operations.Streams.Parameters.StreamId(SystemStreams.AllStream)),
				_ => throw new InvalidOperationException()
			};

			if (!await _provider.CheckAccessAsync(user, op, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}

			await using var enumerator =
				(streamOptionsCase, countOptionsCase, readDirection, filterOptionsCase) switch {
					(StreamOptionOneofCase.Stream,
					CountOptionOneofCase.Count,
					ReadDirection.Forwards,
					FilterOptionOneofCase.NoFilter) => (IAsyncEnumerator<ResolvedEvent>)
					new Enumerators.ReadStreamForwards(
						_publisher,
						request.Options.Stream.StreamIdentifier,
						request.Options.Stream.ToStreamRevision(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						requiresLeader,
						context.Deadline,
						HandleFailure,
						context.CancellationToken),
					(StreamOptionOneofCase.Stream,
					CountOptionOneofCase.Count,
					ReadDirection.Backwards,
					FilterOptionOneofCase.NoFilter) => new Enumerators.ReadStreamBackwards(
						_publisher,
						request.Options.Stream.StreamIdentifier,
						request.Options.Stream.ToStreamRevision(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						requiresLeader,
						context.Deadline,
						HandleFailure,
						context.CancellationToken),
					(StreamOptionOneofCase.All,
					CountOptionOneofCase.Count,
					ReadDirection.Forwards,
					FilterOptionOneofCase.NoFilter) => new Enumerators.ReadAllForwards(
						_publisher,
						request.Options.All.ToPosition(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						requiresLeader,
						context.Deadline,
						context.CancellationToken),
					(StreamOptionOneofCase.All,
					CountOptionOneofCase.Count,
					ReadDirection.Backwards,
					FilterOptionOneofCase.NoFilter) => new Enumerators.ReadAllBackwards(
						_publisher,
						request.Options.All.ToPosition(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						requiresLeader,
						context.Deadline,
						context.CancellationToken),
					(StreamOptionOneofCase.Stream,
					CountOptionOneofCase.Subscription,
					ReadDirection.Forwards,
					FilterOptionOneofCase.NoFilter) => new Enumerators.StreamSubscription(
						_publisher,
						request.Options.Stream.StreamIdentifier,
						request.Options.Stream.ToSubscriptionStreamRevision(),
						request.Options.ResolveLinks,
						user,
						requiresLeader,
						_readIndex,
						context.CancellationToken),
					(StreamOptionOneofCase.All,
					CountOptionOneofCase.Subscription,
					ReadDirection.Forwards,
					FilterOptionOneofCase.NoFilter) => new Enumerators.AllSubscription(
						_publisher,
						request.Options.All.ToSubscriptionPosition(),
						request.Options.ResolveLinks,
						user,
						requiresLeader,
						_readIndex,
						context.CancellationToken),
					(StreamOptionOneofCase.All,
					CountOptionOneofCase.Subscription,
					ReadDirection.Forwards,
					FilterOptionOneofCase.Filter) => new Enumerators.AllSubscriptionFiltered(
						_publisher,
						request.Options.All.ToSubscriptionPosition(),
						request.Options.ResolveLinks,
						ConvertToEventFilter(request.Options.Filter),
						user,
						requiresLeader,
						_readIndex,
						request.Options.Filter.WindowCase switch {
							ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Count => null,
							ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Max => request.Options.Filter.Max,
							_ => throw new InvalidOperationException()
						},
						request.Options.Filter.CheckpointIntervalMultiplier,
						FilteredReadCheckpointReached,
						context.CancellationToken),
					_ => throw new InvalidOperationException()
				};

			await using (context.CancellationToken.Register(() => enumerator.DisposeAsync())) {
				if (enumerator is Enumerators.ISubscriptionEnumerator subscription) {
					await subscription.Started.ConfigureAwait(false);
					await responseStream.WriteAsync(new ReadResp {
						Confirmation = new ReadResp.Types.SubscriptionConfirmation {
							SubscriptionId = subscription.SubscriptionId
						}
					}).ConfigureAwait(false);
				}

				while (await enumerator.MoveNextAsync().ConfigureAwait(false)) {
					await responseStream.WriteAsync(new ReadResp {
						Event = ConvertToReadEvent(enumerator.Current)
					}).ConfigureAwait(false);
				}
			}

			Task HandleFailure(RpcException exception) =>
				exception.StatusCode switch {
					StatusCode.NotFound => responseStream.WriteAsync(new ReadResp {
						StreamNotFound = new ReadResp.Types.StreamNotFound {
							StreamIdentifier = exception.Trailers.First(x => x.Key == Constants.Exceptions.StreamName).Value
						}
					}),
					_ => throw new InvalidOperationException(
						$"Attempted to handle an unknown RPC Exception ({exception.StatusCode})")
				};

			Task FilteredReadCheckpointReached(Position checkpoint)
				=> responseStream.WriteAsync(new ReadResp {
					Checkpoint = new ReadResp.Types.Checkpoint {
						CommitPosition = checkpoint.CommitPosition,
						PreparePosition = checkpoint.PreparePosition
					}
				});

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

			ReadResp.Types.ReadEvent ConvertToReadEvent(ResolvedEvent e) {
				var readEvent = new ReadResp.Types.ReadEvent {
					Link = ConvertToRecordedEvent(e.Link, e.OriginalPosition?.CommitPosition, e.OriginalPosition?.PreparePosition),
					Event = ConvertToRecordedEvent(e.Event, e.OriginalPosition?.CommitPosition, e.OriginalPosition?.PreparePosition)
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

			IEventFilter ConvertToEventFilter(ReadReq.Types.Options.Types.FilterOptions filter) =>
				filter.FilterCase switch {
					ReadReq.Types.Options.Types.FilterOptions.FilterOneofCase.EventType => (
						string.IsNullOrEmpty(filter.EventType.Regex)
							? EventFilter.EventType.Prefixes(filter.EventType.Prefix.ToArray())
							: EventFilter.EventType.Regex(filter.EventType.Regex)),
					ReadReq.Types.Options.Types.FilterOptions.FilterOneofCase.StreamIdentifier => (
						string.IsNullOrEmpty(filter.StreamIdentifier.Regex)
							? EventFilter.StreamName.Prefixes(filter.StreamIdentifier.Prefix.ToArray())
							: EventFilter.StreamName.Regex(filter.StreamIdentifier.Regex)),
					_ => throw new InvalidOperationException()
				};
		}
	}
}
