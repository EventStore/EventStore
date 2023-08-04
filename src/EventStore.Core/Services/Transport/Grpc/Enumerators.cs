using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.Streams;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.UserManagement;
using Google.Protobuf;
using Google.Protobuf.Collections;

namespace EventStore.Core.Services.Transport.Grpc {
	internal static partial class Enumerators {
		private const int MaxLiveEventBufferCount = 32;
		private const int ReadBatchSize = 32; // TODO  JPB make this configurable

		private static readonly BoundedChannelOptions BoundedChannelOptions =
			new(MaxLiveEventBufferCount) {
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = true,
				SingleWriter = true
			};

		private static ReadResp.Types.ReadEvent.Types.RecordedEvent ConvertToRecordedEvent(
			ReadReq.Types.Options.Types.UUIDOption uuidOption, EventRecord e, long? commitPosition,
			long? preparePosition) {
			if (e == null) return null;
			var position = Position.FromInt64(commitPosition ?? -1, preparePosition ?? -1);
			return new ReadResp.Types.ReadEvent.Types.RecordedEvent {
				Id = uuidOption.ContentCase switch {
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

		private static ReadResp.Types.ReadEvent ConvertToReadEvent(ReadReq.Types.Options.Types.UUIDOption uuidOption,
			ResolvedEvent e) {
			var readEvent = new ReadResp.Types.ReadEvent {
				Link = ConvertToRecordedEvent(uuidOption, e.Link, e.LinkPosition?.CommitPosition,
					e.LinkPosition?.PreparePosition),
				Event = ConvertToRecordedEvent(uuidOption, e.Event, e.EventPosition?.CommitPosition,
					e.EventPosition?.PreparePosition),
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

	public abstract class TestEnumerators {
		private IAsyncEnumerator<ReadResp> _enumerator;

		public async Task<SubscriptionResponse> GetNext() {
			if (await _enumerator.MoveNextAsync().ConfigureAwait(false)) {
				return SubscriptionResponse.From(_enumerator.Current);
			}

			return SubscriptionResponse.None;
		}
		
		public class AllSubscription<TStreamId> : TestEnumerators {
			public AllSubscription(SubscriptionRequest req) {
				_enumerator = new Enumerators.AllSubscription(req.Publisher, new DefaultExpiryStrategy(), req.StartPosition, req.ResolveLinks, SystemAccounts.System, false, new FakeReadIndex<TStreamId>(), new ReadReq.Types.Options.Types.UUIDOption(), CancellationToken.None);
			}
		}
		
		public class AllSubscriptionFiltered<TStreamId> : TestEnumerators {
			public AllSubscriptionFiltered(SubscriptionRequest req) {
				_enumerator = new Enumerators.AllSubscriptionFiltered(req.Publisher, new DefaultExpiryStrategy(), req.StartPosition, req.ResolveLinks, req.EventFilter, SystemAccounts.System, false, new FakeReadIndex<TStreamId>(), null, req.CheckpointInterval, new ReadReq.Types.Options.Types.UUIDOption(), CancellationToken.None);
			}
		}
		
		public class StreamSubscription<TStreamId> : TestEnumerators {
			public StreamSubscription(SubscriptionRequest req) {
				_enumerator = new Enumerators.StreamSubscription<TStreamId>(req.Publisher, new DefaultExpiryStrategy(), req.StreamName, req.StartRevision, req.ResolveLinks, SystemAccounts.System, false, new ReadReq.Types.Options.Types.UUIDOption(), CancellationToken.None);
			}
		}

		public record SubscriptionRequest(IQueuedHandler Publisher, Position? StartPosition = null, string StreamName = null, StreamRevision? StartRevision = null, bool ResolveLinks = false, IEventFilter EventFilter = null, uint CheckpointInterval = 1) {
			public readonly IQueuedHandler Publisher = Publisher;
			public readonly Position? StartPosition = StartPosition;
			public readonly string StreamName = StreamName;
			public readonly StreamRevision? StartRevision = StartRevision;
			public readonly bool ResolveLinks = ResolveLinks;
			public readonly IEventFilter EventFilter = EventFilter;
			public readonly uint CheckpointInterval = CheckpointInterval;
		}
		
		public record SubscriptionResponse {
			public static readonly SubscriptionResponse None = new();

			internal static SubscriptionResponse From(ReadResp resp) {
				switch (resp.ContentCase) {
					case ReadResp.ContentOneofCase.None:
					case ReadResp.ContentOneofCase.LastStreamPosition:
					case ReadResp.ContentOneofCase.FirstStreamPosition:
					case ReadResp.ContentOneofCase.LastAllStreamPosition:
					case ReadResp.ContentOneofCase.FellBehind:
						return None;
					case ReadResp.ContentOneofCase.Event:
						return Event.From(resp.Event);
					case ReadResp.ContentOneofCase.Confirmation:
						return SubscriptionConfirmation.From(resp.Confirmation);
					case ReadResp.ContentOneofCase.Checkpoint:
						return Checkpoint.From(resp.Checkpoint);
					case ReadResp.ContentOneofCase.StreamNotFound:
						return StreamNotFound.From(resp.StreamNotFound);
					case ReadResp.ContentOneofCase.CaughtUp:
						return new CaughtUp();
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}
		
		public record Event(RecordedEvent RecordedEvent, RecordedEvent OriginalEvent, RecordedEvent Link, ulong CommitPosition) : SubscriptionResponse {
			public RecordedEvent RecordedEvent = RecordedEvent;
			public RecordedEvent OriginalEvent = OriginalEvent;
			public RecordedEvent Link = Link;
			public ulong CommitPosition = CommitPosition;
			
			internal static Event From(ReadResp.Types.ReadEvent readEvent) {
				return new Event(RecordedEvent.From(readEvent.Event), RecordedEvent.From(readEvent.OriginalEvent), RecordedEvent.From(readEvent.Link), readEvent.CommitPosition);
			}
		}

		public record RecordedEvent(ulong CommitPosition, ulong PreparePosition, ByteString Data, MapField<string, string> Metadata, ByteString CustomMetadata, StreamIdentifier StreamIdentifier, ulong StreamRevision, Uuid Id) {
			public ulong CommitPosition = CommitPosition;
			public ulong PreparePosition = PreparePosition;
			public ByteString Data = Data;
			public MapField<string, string> Metadata = Metadata;
			public ByteString CustomMetadata = CustomMetadata;
			public StreamIdentifier StreamIdentifier = StreamIdentifier;
			public ulong StreamRevision = StreamRevision;
			public Uuid Id = Id;

			private static readonly RecordedEvent _empty = new(0, 0, null, null, null, null, new StreamRevision(), new Uuid());

			internal static RecordedEvent From(ReadResp.Types.ReadEvent.Types.RecordedEvent recordedEvent) {
				if (recordedEvent is null) {
					return _empty;
				}
				return new RecordedEvent(recordedEvent.CommitPosition, recordedEvent.PreparePosition,
					recordedEvent.Data, recordedEvent.Metadata, recordedEvent.CustomMetadata,
					recordedEvent.StreamIdentifier, recordedEvent.StreamRevision, Uuid.FromDto(recordedEvent.Id));
			}
		}

		public record SubscriptionConfirmation(string SubscriptionId) : SubscriptionResponse {
			public string SubscriptionId = SubscriptionId;
			
			internal static SubscriptionConfirmation From(ReadResp.Types.SubscriptionConfirmation confirmation) {
				return new SubscriptionConfirmation(confirmation.SubscriptionId);
			}
		}
		
		public record Checkpoint(ulong CommitPosition, ulong PreparePosition) : SubscriptionResponse {
			public ulong CommitPosition = CommitPosition;
			public ulong PreparePosition = PreparePosition;
			
			internal static Checkpoint From(ReadResp.Types.Checkpoint checkpoint) {
				return new Checkpoint(checkpoint.CommitPosition, checkpoint.PreparePosition);
			}
		}
		
		public record StreamNotFound(StreamIdentifier StreamIdentifier) : SubscriptionResponse {
			public StreamIdentifier StreamIdentifier = StreamIdentifier;

			internal static StreamNotFound From(ReadResp.Types.StreamNotFound streamNotFound) {
				return new StreamNotFound(streamNotFound.StreamIdentifier);
			}
		}
		
		public record CaughtUp : SubscriptionResponse {
		}

		private class FakeReadIndex<TStreamId> : IReadIndex<TStreamId> {
			public long LastIndexedPosition { get; }
			public ReadIndexStats GetStatistics() {
				return null;
			}

			public IndexReadAllResult ReadAllEventsForward(TFPos pos, int maxCount) {
				return default;
			}

			public IndexReadAllResult ReadAllEventsBackward(TFPos pos, int maxCount) {
				return default;
			}

			public IndexReadAllResult ReadAllEventsForwardFiltered(TFPos pos, int maxCount, int maxSearchWindow, IEventFilter eventFilter) {
				return default;
			}

			public IndexReadAllResult ReadAllEventsBackwardFiltered(TFPos pos, int maxCount, int maxSearchWindow, IEventFilter eventFilter) {
				return default;
			}

			public void Close() {
			}

			public void Dispose() {
			}

			public IIndexWriter<TStreamId> IndexWriter { get; }
			public IndexReadEventResult ReadEvent(string streamName, TStreamId streamId, long eventNumber) {
				return default;
			}

			public IndexReadStreamResult ReadStreamEventsBackward(string streamName, TStreamId streamId, long fromEventNumber,
				int maxCount) {
				return default;
			}

			public IndexReadStreamResult
				ReadStreamEventsForward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount) {
				return default;
			}

			public IndexReadEventInfoResult ReadEventInfo_KeepDuplicates(TStreamId streamId, long eventNumber) {
				return default;
			}

			public IndexReadEventInfoResult ReadEventInfoForward_KnownCollisions(TStreamId streamId, long fromEventNumber, int maxCount,
				long beforePosition) {
				return default;
			}

			public IndexReadEventInfoResult ReadEventInfoForward_NoCollisions(ulong stream, long fromEventNumber, int maxCount,
				long beforePosition) {
				return default;
			}

			public IndexReadEventInfoResult ReadEventInfoBackward_KnownCollisions(TStreamId streamId, long fromEventNumber, int maxCount,
				long beforePosition) {
				return default;
			}

			public IndexReadEventInfoResult ReadEventInfoBackward_NoCollisions(ulong stream, Func<ulong, TStreamId> getStreamId, long fromEventNumber,
				int maxCount, long beforePosition) {
				return default;
			}

			public bool IsStreamDeleted(TStreamId streamId) {
				return true;
			}

			public long GetStreamLastEventNumber(TStreamId streamId) {
				return default;
			}

			public long GetStreamLastEventNumber_KnownCollisions(TStreamId streamId, long beforePosition) {
				return default;
			}

			public long GetStreamLastEventNumber_NoCollisions(ulong stream, Func<ulong, TStreamId> getStreamId, long beforePosition) {
				return default;
			}

			public StreamMetadata GetStreamMetadata(TStreamId streamId) {
				return default;
			}

			public StorageMessage.EffectiveAcl GetEffectiveAcl(TStreamId streamId) {
				return default;
			}

			public TStreamId GetEventStreamIdByTransactionId(long transactionId) {
				return default;
			}

			public TStreamId GetStreamId(string streamName) {
				return default;
			}

			public string GetStreamName(TStreamId streamId) {
				return default;
			}
		}
	}
}
