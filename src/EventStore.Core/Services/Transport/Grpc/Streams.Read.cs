using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Util;
using EventStore.Client;
using EventStore.Client.Streams;
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

			var user = await GetUser(_authenticationProvider, context.RequestHeaders).ConfigureAwait(false);

			await using var enumerator =
				(streamOptionsCase, countOptionsCase, readDirection, filterOptionsCase) switch {
					(StreamOptionOneofCase.Stream,
					CountOptionOneofCase.Count,
					ReadDirection.Forwards,
					FilterOptionOneofCase.NoFilter) => (IAsyncEnumerator<ResolvedEvent>)
					new Enumerators.ReadStreamForwards(
						_queue,
						request.Options.Stream.StreamName,
						request.Options.Stream.ToStreamRevision(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						context.CancellationToken),
					(StreamOptionOneofCase.Stream,
					CountOptionOneofCase.Count,
					ReadDirection.Backwards,
					FilterOptionOneofCase.NoFilter) => new Enumerators.ReadStreamBackwards(
						_queue,
						request.Options.Stream.StreamName,
						request.Options.Stream.ToStreamRevision(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						context.CancellationToken),
					(StreamOptionOneofCase.All,
					CountOptionOneofCase.Count,
					ReadDirection.Forwards,
					FilterOptionOneofCase.NoFilter) => new Enumerators.ReadAllForwards(
						_queue,
						request.Options.All.ToPosition(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						context.CancellationToken),
					(StreamOptionOneofCase.All,
					CountOptionOneofCase.Count,
					ReadDirection.Forwards,
					FilterOptionOneofCase.Filter) => new Enumerators.ReadAllForwardsFiltered(
						_queue,
						request.Options.All.ToPosition(),
						request.Options.Count,
						request.Options.ResolveLinks,
						ConvertToEventFilter(request.Options.Filter),
						request.Options.Filter.WindowCase switch {
							ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Count => null,
							ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Max => request.Options.Filter.Max,
							_ => throw new InvalidOperationException()
						},
						user,
						context.CancellationToken),
					(StreamOptionOneofCase.All,
					CountOptionOneofCase.Count,
					ReadDirection.Backwards,
					FilterOptionOneofCase.NoFilter) => new Enumerators.ReadAllBackwards(
						_queue,
						request.Options.All.ToPosition(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						context.CancellationToken),
					(StreamOptionOneofCase.All,
					CountOptionOneofCase.Count,
					ReadDirection.Backwards,
					FilterOptionOneofCase.Filter) => new Enumerators.ReadAllBackwardsFiltered(
						_queue,
						request.Options.All.ToPosition(),
						request.Options.Count,
						request.Options.ResolveLinks,
						ConvertToEventFilter(request.Options.Filter),
						request.Options.Filter.WindowCase switch {
							ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Count => null,
							ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Max => request.Options.Filter.Max,
							_ => throw new InvalidOperationException()
						},
						user,
						context.CancellationToken),
					(StreamOptionOneofCase.Stream,
					CountOptionOneofCase.Subscription,
					ReadDirection.Forwards,
					FilterOptionOneofCase.NoFilter) => new Enumerators.StreamSubscription(
						_queue,
						request.Options.Stream.StreamName,
						request.Options.Stream.ToSubscriptionStreamRevision(),
						request.Options.ResolveLinks,
						user,
						_readIndex,
						context.CancellationToken),
					(StreamOptionOneofCase.All,
					CountOptionOneofCase.Subscription,
					ReadDirection.Forwards,
					FilterOptionOneofCase.NoFilter) => new Enumerators.AllSubscription(
						_queue,
						request.Options.All.ToSubscriptionPosition(),
						request.Options.ResolveLinks,
						user,
						_readIndex,
						context.CancellationToken),
					(StreamOptionOneofCase.All,
					CountOptionOneofCase.Subscription,
					ReadDirection.Forwards,
					FilterOptionOneofCase.Filter) => new Enumerators.AllSubscriptionFiltered(
						_queue,
						request.Options.All.ToSubscriptionPosition(),
						request.Options.ResolveLinks,
						ConvertToEventFilter(request.Options.Filter),
						user,
						_readIndex,
						request.Options.Filter.WindowCase switch {
							ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Count => null,
							ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Max => request.Options.Filter.Max
						},
						request.Options.Filter.CheckpointIntervalMultiplier,
						CheckpointReached,
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

			Task CheckpointReached(Position checkpoint)
				=> responseStream.WriteAsync(new ReadResp {
					Checkpoint = new ReadResp.Types.Checkpoint {
						CommitPosition = checkpoint.CommitPosition,
						PreparePosition = checkpoint.PreparePosition
					}
				});

			ReadResp.Types.ReadEvent.Types.RecordedEvent ConvertToRecordedEvent(EventRecord e, long? commitPosition) {
				if (e == null) return null;
				var position = Position.FromInt64(commitPosition ?? e.LogPosition, e.TransactionPosition);
				return new ReadResp.Types.ReadEvent.Types.RecordedEvent {
					Id = uuidOptionsCase switch {
						ReadReq.Types.Options.Types.UUIDOption.ContentOneofCase.String => new UUID {
							String = e.EventId.ToString()
						},
						_ => Uuid.FromGuid(e.EventId).ToStreamsDto()
					},
					StreamName = e.EventStreamId,
					StreamRevision = StreamRevision.FromInt64(e.EventNumber),
					CommitPosition = position.CommitPosition,
					PreparePosition = position.PreparePosition,
					Metadata = {
						[Constants.Metadata.Type] = e.EventType,
						[Constants.Metadata.IsJson] = e.IsJson.ToString(),
						[Constants.Metadata.Created] = EpochExtensions.ToTicksSinceEpoch(e.TimeStamp).ToString()
					},
					Data = ByteString.CopyFrom(e.Data),
					CustomMetadata = ByteString.CopyFrom(e.Metadata)
				};
			}

			ReadResp.Types.ReadEvent ConvertToReadEvent(ResolvedEvent e) {
				var readEvent = new ReadResp.Types.ReadEvent {
					Link = ConvertToRecordedEvent(e.Link, e.OriginalPosition?.CommitPosition),
					Event = ConvertToRecordedEvent(e.Event, e.OriginalPosition?.CommitPosition)
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

			Util.IEventFilter ConvertToEventFilter(ReadReq.Types.Options.Types.FilterOptions filter) =>
				filter.FilterCase switch {
					ReadReq.Types.Options.Types.FilterOptions.FilterOneofCase.EventType => (
						string.IsNullOrEmpty(filter.EventType.Regex)
							? EventFilter.EventType.Prefixes(filter.EventType.Prefix.ToArray())
							: EventFilter.EventType.Regex(filter.EventType.Regex)),
					ReadReq.Types.Options.Types.FilterOptions.FilterOneofCase.StreamName => (
						string.IsNullOrEmpty(filter.StreamName.Regex)
							? EventFilter.StreamName.Prefixes(filter.StreamName.Prefix.ToArray())
							: EventFilter.StreamName.Regex(filter.StreamName.Regex)),
					_ => throw new InvalidOperationException()
				};
		}
	}
}
