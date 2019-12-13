using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Util;
using EventStore.Grpc;
using EventStore.Grpc.Streams;
using Google.Protobuf;
using Grpc.Core;
using CountOptionsOneofCase = EventStore.Grpc.Streams.ReadReq.Types.Options.CountOptionsOneofCase;
using FilterOptionsOneofCase = EventStore.Grpc.Streams.ReadReq.Types.Options.FilterOptionsOneofCase;
using ReadDirection = EventStore.Grpc.Streams.ReadReq.Types.Options.Types.ReadDirection;
using StreamOptionsOneofCase = EventStore.Grpc.Streams.ReadReq.Types.Options.StreamOptionsOneofCase;
using UUID = EventStore.Grpc.Streams.UUID;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Streams {
		public override async Task Read(
			ReadReq request,
			IServerStreamWriter<ReadResp> responseStream,
			ServerCallContext context) {
			var options = request.Options;
			var countOptionsCase = options.CountOptionsCase;
			var streamOptionsCase = options.StreamOptionsCase;
			var readDirection = options.ReadDirection;
			var filterOptionsCase = options.FilterOptionsCase;

			var user = await GetUser(_authenticationProvider, context.RequestHeaders).ConfigureAwait(false);

			await using var enumerator =
				(streamOptionsCase, countOptionsCase, readDirection, filterOptionsCase) switch {
					(StreamOptionsOneofCase.Stream,
					CountOptionsOneofCase.Count,
					ReadDirection.Forwards,
					FilterOptionsOneofCase.NoFilter) => (
						IAsyncEnumerator<(ResolvedEvent? resolvedEvent, Position? position)>)
					new Enumerators.ReadStreamForwards(
						_queue,
						request.Options.Stream.StreamName,
						request.Options.Stream.ToStreamRevision(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						context.CancellationToken),
					(StreamOptionsOneofCase.Stream,
					CountOptionsOneofCase.Count,
					ReadDirection.Backwards,
					FilterOptionsOneofCase.NoFilter) => new Enumerators.ReadStreamBackwards(
						_queue,
						request.Options.Stream.StreamName,
						request.Options.Stream.ToStreamRevision(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						context.CancellationToken),
					(StreamOptionsOneofCase.All,
					CountOptionsOneofCase.Count,
					ReadDirection.Forwards,
					FilterOptionsOneofCase.NoFilter) => new Enumerators.ReadAllForwards(
						_queue,
						request.Options.All.ToPosition(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						context.CancellationToken),
					(StreamOptionsOneofCase.All,
					CountOptionsOneofCase.Count,
					ReadDirection.Forwards,
					FilterOptionsOneofCase.Filter) => new Enumerators.ReadAllForwardsFiltered(
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
					(StreamOptionsOneofCase.All,
					CountOptionsOneofCase.Count,
					ReadDirection.Backwards,
					FilterOptionsOneofCase.NoFilter) => new Enumerators.ReadAllBackwards(
						_queue,
						request.Options.All.ToPosition(),
						request.Options.Count,
						request.Options.ResolveLinks,
						user,
						context.CancellationToken),
					(StreamOptionsOneofCase.All,
					CountOptionsOneofCase.Count,
					ReadDirection.Backwards,
					FilterOptionsOneofCase.Filter) => new Enumerators.ReadAllBackwardsFiltered(
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
					(StreamOptionsOneofCase.Stream,
					CountOptionsOneofCase.Subscription,
					ReadDirection.Forwards,
					FilterOptionsOneofCase.NoFilter) => new Enumerators.StreamSubscription(
						_queue,
						request.Options.Stream.StreamName,
						request.Options.Stream.ToStreamRevision(),
						request.Options.ResolveLinks,
						user,
						_readIndex,
						context.CancellationToken),
					(StreamOptionsOneofCase.All,
					CountOptionsOneofCase.Subscription,
					ReadDirection.Forwards,
					FilterOptionsOneofCase.NoFilter) => new Enumerators.AllSubscription(
						_queue,
						request.Options.All.ToPosition(),
						request.Options.ResolveLinks,
						user,
						_readIndex,
						context.CancellationToken),
					(StreamOptionsOneofCase.All,
					CountOptionsOneofCase.Subscription,
					ReadDirection.Forwards,
					FilterOptionsOneofCase.Filter) => new Enumerators.AllSubscriptionFiltered(
						_queue,
						request.Options.All.ToPosition(),
						request.Options.ResolveLinks,
						ConvertToEventFilter(request.Options.Filter),
						request.Options.CheckpointInterval,
						user,
						_readIndex,
						context.CancellationToken),
					_ => throw new InvalidOperationException()
				};

			while (await enumerator.MoveNextAsync().ConfigureAwait(false)) {
				await responseStream.WriteAsync(
					(enumerator.Current.resolvedEvent.HasValue, enumerator.Current.position.HasValue) switch {
						(true, false) => new ReadResp {
							Event = ConvertToReadEvent(enumerator.Current.resolvedEvent.Value)
						},
						(false, true) => new ReadResp {
							CheckpointReached = new ReadResp.Types.CheckpointReached {
								CommitPosition = enumerator.Current.position.Value.CommitPosition,
								PreparePosition = enumerator.Current.position.Value.PreparePosition
							}
						}
					}).ConfigureAwait(false);
			}

			ReadResp.Types.ReadEvent.Types.RecordedEvent ConvertToRecordedEvent(EventRecord e, long? commitPosition) {
				if (e == null) return null;
				var position = Position.FromInt64(commitPosition ?? e.LogPosition, e.TransactionPosition);
				return new ReadResp.Types.ReadEvent.Types.RecordedEvent {
					Id = Uuid.FromGuid(e.EventId).ToStreamsDto(),
					StreamName = e.EventStreamId,
					StreamRevision = StreamRevision.FromInt64(e.EventNumber),
					CommitPosition = position.CommitPosition,
					PreparePosition = position.PreparePosition,
					Metadata = {
						[Constants.Metadata.Type] = e.EventType,
						[Constants.Metadata.IsJson] = e.IsJson.ToString(),
						[Constants.Metadata.Created] = e.TimeStamp.ToTicksSinceEpoch().ToString()
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
