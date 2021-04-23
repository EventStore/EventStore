using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IIndexReader<TStreamId> {
		long CachedStreamInfo { get; }
		long NotCachedStreamInfo { get; }
		long HashCollisions { get; }

		// streamId drives the read, streamName is only for populating on the result.
		// this was less messy than safely adding the streamName to the EventRecord at some point after construction
		IndexReadEventResult ReadEvent(string streamName, TStreamId streamId, long eventNumber);
		IndexReadStreamResult ReadStreamEventsForward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount);
		IndexReadStreamResult ReadStreamEventsBackward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount);
		StorageMessage.EffectiveAcl GetEffectiveAcl(TStreamId streamId);
		/// <summary>
		/// Doesn't filter $maxAge, $maxCount, $tb(truncate before), doesn't check stream deletion, etc.
		/// </summary>
		IPrepareLogRecord<TStreamId> ReadPrepare(TStreamId streamId, long eventNumber);

		TStreamId GetEventStreamIdByTransactionId(long transactionId);

		StreamMetadata GetStreamMetadata(TStreamId streamId);
		long GetStreamLastEventNumber(TStreamId streamId);
	}

	public abstract class IndexReader {
		public static string UnspecifiedStreamName { get; } = "Unspecified stream name";
		protected static readonly ILogger Log = Serilog.Log.ForContext<IndexReader>();
	}

	public class IndexReader<TStreamId> : IndexReader, IIndexReader<TStreamId> {
		private static EqualityComparer<TStreamId> StreamIdComparer { get; } = EqualityComparer<TStreamId>.Default;

		public long CachedStreamInfo {
			get { return Interlocked.Read(ref _cachedStreamInfo); }
		}

		public long NotCachedStreamInfo {
			get { return Interlocked.Read(ref _notCachedStreamInfo); }
		}

		public long HashCollisions {
			get { return Interlocked.Read(ref _hashCollisions); }
		}

		private readonly IIndexBackend<TStreamId> _backend;
		private readonly ITableIndex<TStreamId> _tableIndex;
		private readonly ISystemStreamLookup<TStreamId> _systemStreams;
		private readonly IValidator<TStreamId> _validator;
		private readonly bool _skipIndexScanOnRead;
		private readonly StreamMetadata _metastreamMetadata;

		private long _hashCollisions;
		private long _cachedStreamInfo;
		private long _notCachedStreamInfo;
		private int _hashCollisionReadLimit;

		public IndexReader(
			IIndexBackend<TStreamId> backend,
			ITableIndex<TStreamId> tableIndex,
			IStreamNamesProvider<TStreamId> streamNamesProvider,
			IValidator<TStreamId> validator,
			StreamMetadata metastreamMetadata,
			int hashCollisionReadLimit, bool skipIndexScanOnRead) {
			Ensure.NotNull(backend, "backend");
			Ensure.NotNull(tableIndex, "tableIndex");
			Ensure.NotNull(streamNamesProvider, nameof(streamNamesProvider));
			Ensure.NotNull(validator, nameof(validator));
			Ensure.NotNull(metastreamMetadata, "metastreamMetadata");

			streamNamesProvider.SetReader(this);

			_backend = backend;
			_tableIndex = tableIndex;
			_systemStreams = streamNamesProvider.SystemStreams;
			_validator = validator;
			_metastreamMetadata = metastreamMetadata;
			_hashCollisionReadLimit = hashCollisionReadLimit;
			_skipIndexScanOnRead = skipIndexScanOnRead;
		}

		IndexReadEventResult IIndexReader<TStreamId>.ReadEvent(string streamName, TStreamId streamId, long eventNumber) {
			Ensure.Valid(streamId, _validator);
			if (eventNumber < -1) throw new ArgumentOutOfRangeException("eventNumber");
			using (var reader = _backend.BorrowReader()) {
				return ReadEventInternal(reader, streamName, streamId, eventNumber);
			}
		}

		private IndexReadEventResult ReadEventInternal(TFReaderLease reader, string streamName, TStreamId streamId, long eventNumber) {
			var lastEventNumber = GetStreamLastEventNumberCached(reader, streamId);
			var metadata = GetStreamMetadataCached(reader, streamId);
			var originalStreamExists = OriginalStreamExists(reader, streamId);
			if (lastEventNumber == EventNumber.DeletedStream)
				return new IndexReadEventResult(ReadEventResult.StreamDeleted, metadata, lastEventNumber,
					originalStreamExists);
			if (lastEventNumber == ExpectedVersion.NoStream || metadata.TruncateBefore == EventNumber.DeletedStream)
				return new IndexReadEventResult(ReadEventResult.NoStream, metadata,lastEventNumber,
					originalStreamExists);
			if (lastEventNumber == EventNumber.Invalid)
				return new IndexReadEventResult(ReadEventResult.NoStream, metadata, lastEventNumber,
					originalStreamExists);

			if (eventNumber == -1)
				eventNumber = lastEventNumber;

			long minEventNumber = 0;
			if (metadata.MaxCount.HasValue)
				minEventNumber = Math.Max(minEventNumber, lastEventNumber - metadata.MaxCount.Value + 1);
			if (metadata.TruncateBefore.HasValue)
				minEventNumber = Math.Max(minEventNumber, metadata.TruncateBefore.Value);
			//TODO(clc): confirm this logic, it seems that reads less than min should be invaild rather than found
			if (eventNumber < minEventNumber || eventNumber > lastEventNumber)
				return new IndexReadEventResult(ReadEventResult.NotFound, metadata,  lastEventNumber,
					originalStreamExists);

			var prepare = ReadPrepareInternal(reader, streamId, eventNumber);
			if (prepare != null) {
				if (metadata.MaxAge.HasValue && prepare.TimeStamp < DateTime.UtcNow - metadata.MaxAge.Value)
					return new IndexReadEventResult(ReadEventResult.NotFound, metadata,  lastEventNumber,
						originalStreamExists);
				return new IndexReadEventResult(ReadEventResult.Success, new EventRecord(eventNumber, prepare, streamName),
					metadata, lastEventNumber, originalStreamExists);
			}

			return new IndexReadEventResult(ReadEventResult.NotFound, metadata, lastEventNumber,
				originalStreamExists: originalStreamExists);
		}

		IPrepareLogRecord<TStreamId> IIndexReader<TStreamId>.ReadPrepare(TStreamId streamId, long eventNumber) {
			using (var reader = _backend.BorrowReader()) {
				return ReadPrepareInternal(reader, streamId, eventNumber);
			}
		}

		private IPrepareLogRecord<TStreamId> ReadPrepareInternal(TFReaderLease reader, TStreamId streamId, long eventNumber) {
			// we assume that you already did check for stream deletion
			Ensure.Valid(streamId, _validator);
			Ensure.Nonnegative(eventNumber, "eventNumber");

			return _skipIndexScanOnRead
				? ReadPrepareSkipScan(reader, streamId, eventNumber)
				: ReadPrepare(reader, streamId, eventNumber);
		}

		private IPrepareLogRecord<TStreamId> ReadPrepare(TFReaderLease reader, TStreamId streamId, long eventNumber) {
			var recordsQuery = _tableIndex.GetRange(streamId, eventNumber, eventNumber)
				.Select(x => new {x.Version, Prepare = ReadPrepareInternal(reader, x.Position)})
				.Where(x => x.Prepare != null && StreamIdComparer.Equals(x.Prepare.EventStreamId, streamId))
				.GroupBy(x => x.Version).Select(x => x.Last()).ToList();
			if (recordsQuery.Count() == 1) {
				return recordsQuery.First().Prepare;
			}

			return null;
		}

		private IPrepareLogRecord<TStreamId> ReadPrepareSkipScan(TFReaderLease reader, TStreamId streamId, long eventNumber) {
			long position;
			if (_tableIndex.TryGetOneValue(streamId, eventNumber, out position)) {
				var rec = ReadPrepareInternal(reader, position);
				if (rec != null && StreamIdComparer.Equals(rec.EventStreamId, streamId))
					return rec;

				foreach (var indexEntry in _tableIndex.GetRange(streamId, eventNumber, eventNumber)) {
					Interlocked.Increment(ref _hashCollisions);
					if (indexEntry.Position == position)
						continue;
					rec = ReadPrepareInternal(reader, indexEntry.Position);
					if (rec != null && StreamIdComparer.Equals(rec.EventStreamId, streamId))
						return rec;
				}
			}

			return null;
		}

		protected static IPrepareLogRecord<TStreamId> ReadPrepareInternal(TFReaderLease reader, long logPosition) {
			RecordReadResult result = reader.TryReadAt(logPosition);
			if (!result.Success)
				return null;

			if (result.LogRecord.RecordType != LogRecordType.Prepare)
				throw new Exception(string.Format("Incorrect type of log record {0}, expected Prepare record.",
					result.LogRecord.RecordType));
			return (IPrepareLogRecord<TStreamId>)result.LogRecord;
		}

		IndexReadStreamResult IIndexReader<TStreamId>.
			ReadStreamEventsForward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount) {
			return ReadStreamEventsForwardInternal(streamName, streamId, fromEventNumber, maxCount, _skipIndexScanOnRead);
		}

		private IndexReadStreamResult ReadStreamEventsForwardInternal(string streamName, TStreamId streamId, long fromEventNumber,
			int maxCount, bool skipIndexScanOnRead) {
			Ensure.Valid(streamId, _validator);
			Ensure.Nonnegative(fromEventNumber, "fromEventNumber");
			Ensure.Positive(maxCount, "maxCount");

			using (var reader = _backend.BorrowReader()) {
				var lastEventNumber = GetStreamLastEventNumberCached(reader, streamId);
				var metadata = GetStreamMetadataCached(reader, streamId);
				if (lastEventNumber == EventNumber.DeletedStream)
					return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.StreamDeleted,
						StreamMetadata.Empty, lastEventNumber);
				if (lastEventNumber == ExpectedVersion.NoStream || metadata.TruncateBefore == EventNumber.DeletedStream)
					return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.NoStream, metadata,
						lastEventNumber);
				if (lastEventNumber == EventNumber.Invalid)
					return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.NoStream, metadata,
						lastEventNumber);

				long startEventNumber = fromEventNumber;
				long endEventNumber = Math.Min(long.MaxValue, fromEventNumber + maxCount - 1);

				long minEventNumber = 0;
				if (metadata.MaxCount.HasValue)
					minEventNumber = Math.Max(minEventNumber, lastEventNumber - metadata.MaxCount.Value + 1);
				if (metadata.TruncateBefore.HasValue)
					minEventNumber = Math.Max(minEventNumber, metadata.TruncateBefore.Value);
				if (endEventNumber < minEventNumber)
					return new IndexReadStreamResult(fromEventNumber, maxCount, IndexReadStreamResult.EmptyRecords,
						metadata, minEventNumber, lastEventNumber, isEndOfStream: false);
				startEventNumber = Math.Max(startEventNumber, minEventNumber);

				var recordsQuery = _tableIndex.GetRange(streamId, startEventNumber, endEventNumber)
					.Select(x => new {x.Version, Prepare = ReadPrepareInternal(reader, x.Position)})
					.Where(x => x.Prepare != null && StreamIdComparer.Equals(x.Prepare.EventStreamId, streamId));
				if (!skipIndexScanOnRead) {
					recordsQuery = recordsQuery.OrderByDescending(x => x.Version)
						.GroupBy(x => x.Version).Select(x => x.Last());
				}

				if (metadata.MaxAge.HasValue) {
					var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
					recordsQuery = recordsQuery.Where(x => x.Prepare.TimeStamp >= ageThreshold);
				}

				var records = recordsQuery.Reverse().Select(x => new EventRecord(x.Version, x.Prepare, streamName)).ToArray();

				long nextEventNumber = Math.Min(endEventNumber + 1, lastEventNumber + 1);
				if (records.Length > 0)
					nextEventNumber = records[records.Length - 1].EventNumber + 1;
				var isEndOfStream = endEventNumber >= lastEventNumber;
				return new IndexReadStreamResult(endEventNumber, maxCount, records, metadata,
					nextEventNumber, lastEventNumber, isEndOfStream);
			}
		}

		IndexReadStreamResult IIndexReader<TStreamId>.
			ReadStreamEventsBackward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount) {
			return ReadStreamEventsBackwardInternal(streamName, streamId, fromEventNumber, maxCount, _skipIndexScanOnRead);
		}

		private IndexReadStreamResult ReadStreamEventsBackwardInternal(string streamName, TStreamId streamId, long fromEventNumber,
			int maxCount, bool skipIndexScanOnRead) {
			Ensure.Valid(streamId, _validator);
			Ensure.Positive(maxCount, "maxCount");

			using (var reader = _backend.BorrowReader()) {
				var lastEventNumber = GetStreamLastEventNumberCached(reader, streamId);
				var metadata = GetStreamMetadataCached(reader, streamId);
				if (lastEventNumber == EventNumber.DeletedStream)
					return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.StreamDeleted,
						StreamMetadata.Empty, lastEventNumber);
				if (lastEventNumber == ExpectedVersion.NoStream || metadata.TruncateBefore == EventNumber.DeletedStream)
					return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.NoStream, metadata,
						lastEventNumber);
				if (lastEventNumber == EventNumber.Invalid)
					return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.NoStream, metadata,
						lastEventNumber);

				long endEventNumber = fromEventNumber < 0 ? lastEventNumber : fromEventNumber;
				long startEventNumber = Math.Max(0L, endEventNumber - maxCount + 1);
				bool isEndOfStream = false;

				long minEventNumber = 0;
				if (metadata.MaxCount.HasValue)
					minEventNumber = Math.Max(minEventNumber, lastEventNumber - metadata.MaxCount.Value + 1);
				if (metadata.TruncateBefore.HasValue)
					minEventNumber = Math.Max(minEventNumber, metadata.TruncateBefore.Value);
				if (endEventNumber < minEventNumber)
					return new IndexReadStreamResult(fromEventNumber, maxCount, IndexReadStreamResult.EmptyRecords,
						metadata, -1, lastEventNumber, isEndOfStream: true);

				if (startEventNumber <= minEventNumber) {
					isEndOfStream = true;
					startEventNumber = minEventNumber;
				}

				var recordsQuery = _tableIndex.GetRange(streamId, startEventNumber, endEventNumber)
					.Select(x => new {x.Version, Prepare = ReadPrepareInternal(reader, x.Position)})
					.Where(x => x.Prepare != null && StreamIdComparer.Equals(x.Prepare.EventStreamId, streamId));
				if (!skipIndexScanOnRead) {
					recordsQuery = recordsQuery.OrderByDescending(x => x.Version)
						.GroupBy(x => x.Version).Select(x => x.Last());
					;
				}

				if (metadata.MaxAge.HasValue) {
					var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
					recordsQuery = recordsQuery.Where(x => x.Prepare.TimeStamp >= ageThreshold);
				}

				var records = recordsQuery.Select(x => new EventRecord(x.Version, x.Prepare, streamName)).ToArray();

				isEndOfStream = isEndOfStream
				                || startEventNumber == 0
				                || (startEventNumber <= lastEventNumber
				                    && (records.Length == 0 ||
				                        records[records.Length - 1].EventNumber != startEventNumber));
				long nextEventNumber = isEndOfStream ? -1 : Math.Min(startEventNumber - 1, lastEventNumber);
				return new IndexReadStreamResult(endEventNumber, maxCount, records, metadata,
					nextEventNumber, lastEventNumber, isEndOfStream);
			}
		}

		public TStreamId GetEventStreamIdByTransactionId(long transactionId) {
			Ensure.Nonnegative(transactionId, "transactionId");
			using (var reader = _backend.BorrowReader()) {
				var res = ReadPrepareInternal(reader, transactionId);
				return res == null ? default : res.EventStreamId;
			}
		}

		public StorageMessage.EffectiveAcl GetEffectiveAcl(TStreamId streamId) {
			using (var reader = _backend.BorrowReader()) {
				var sysSettings = _backend.GetSystemSettings() ?? SystemSettings.Default;
				StreamAcl acl;
				StreamAcl sysAcl;
				StreamAcl defAcl;
				var meta = GetStreamMetadataCached(reader, streamId);
				if (_systemStreams.IsSystemStream(streamId)) {
					defAcl = SystemSettings.Default.SystemStreamAcl;
					sysAcl = sysSettings.SystemStreamAcl ?? defAcl;
					acl = meta.Acl ?? sysAcl;
				} else {
					defAcl = SystemSettings.Default.UserStreamAcl;
					sysAcl = sysSettings.UserStreamAcl ?? defAcl;
					acl = meta.Acl ?? sysAcl;
				}
				return new StorageMessage.EffectiveAcl(acl, sysAcl, defAcl);
			}
		}
		
		long IIndexReader<TStreamId>.GetStreamLastEventNumber(TStreamId streamId) {
			Ensure.Valid(streamId, _validator);
			using (var reader = _backend.BorrowReader()) {
				return GetStreamLastEventNumberCached(reader, streamId);
			}
		}

		StreamMetadata IIndexReader<TStreamId>.GetStreamMetadata(TStreamId streamId) {
			Ensure.Valid(streamId, _validator);
			using (var reader = _backend.BorrowReader()) {
				return GetStreamMetadataCached(reader, streamId);
			}
		}

		private long GetStreamLastEventNumberCached(TFReaderLease reader, TStreamId streamId) {
			// if this is metastream -- check if original stream was deleted, if yes -- metastream is deleted as well
			if (_systemStreams.IsMetaStream(streamId)
			    && GetStreamLastEventNumberCached(reader, _systemStreams.OriginalStreamOf(streamId)) ==
			    EventNumber.DeletedStream)
				return EventNumber.DeletedStream;

			var cache = _backend.TryGetStreamLastEventNumber(streamId);
			if (cache.LastEventNumber != null) {
				Interlocked.Increment(ref _cachedStreamInfo);
				return cache.LastEventNumber.GetValueOrDefault();
			}

			Interlocked.Increment(ref _notCachedStreamInfo);
			var lastEventNumber = GetStreamLastEventNumberUncached(reader, streamId);

			// Conditional update depending on previously returned cache info version.
			// If version is not correct -- nothing is changed in cache.
			// This update is conditioned to not interfere with updating stream cache info by commit procedure
			// (which is the source of truth).
			var res = _backend.UpdateStreamLastEventNumber(cache.Version, streamId, lastEventNumber);
			return res ?? lastEventNumber;
		}

		private long GetStreamLastEventNumberUncached(TFReaderLease reader, TStreamId streamId) {
			IndexEntry latestEntry;
			if (!_tableIndex.TryGetLatestEntry(streamId, out latestEntry))
				return ExpectedVersion.NoStream;

			var rec = ReadPrepareInternal(reader, latestEntry.Position);
			if (rec == null)
				throw new Exception(
					$"Could not read latest stream's prepare for stream '{streamId}' at position {latestEntry.Position}");

			int count = 0;
			long startVersion = 0;
			long latestVersion = long.MinValue;
			if (StreamIdComparer.Equals(rec.EventStreamId, streamId)) {
				startVersion = Math.Max(latestEntry.Version, latestEntry.Version + 1);
				latestVersion = latestEntry.Version;
			}

			foreach (var indexEntry in _tableIndex.GetRange(streamId, startVersion, long.MaxValue,
				limit: _hashCollisionReadLimit + 1)) {
				var r = ReadPrepareInternal(reader, indexEntry.Position);
				if (r != null && StreamIdComparer.Equals(r.EventStreamId, streamId)) {
					if (latestVersion == long.MinValue) {
						latestVersion = indexEntry.Version;
						continue;
					}

					return latestVersion < indexEntry.Version ? indexEntry.Version : latestVersion;
				}

				count++;
				Interlocked.Increment(ref _hashCollisions);
				if (count > _hashCollisionReadLimit) {
					Log.Error("A hash collision resulted in not finding the last event number for the stream {stream}.",
						streamId);
					return EventNumber.Invalid;
				}
			}

			return latestVersion == long.MinValue ? ExpectedVersion.NoStream : latestVersion;
		}

		private bool OriginalStreamExists(TFReaderLease reader, TStreamId metaStreamId) {
			if (_systemStreams.IsMetaStream(metaStreamId)) {
				var originalStreamId = _systemStreams.OriginalStreamOf(metaStreamId);
				var lastEventNumber = GetStreamLastEventNumberCached(reader, originalStreamId);
				if (lastEventNumber == ExpectedVersion.NoStream || lastEventNumber == EventNumber.DeletedStream)
					return false;
				return true;
			}

			return false;
		}

		private StreamMetadata GetStreamMetadataCached(TFReaderLease reader, TStreamId streamId) {
			// if this is metastream -- check if original stream was deleted, if yes -- metastream is deleted as well
			if (_systemStreams.IsMetaStream(streamId))
				return _metastreamMetadata;

			var cache = _backend.TryGetStreamMetadata(streamId);
			if (cache.Metadata != null) {
				Interlocked.Increment(ref _cachedStreamInfo);
				return cache.Metadata;
			}

			Interlocked.Increment(ref _notCachedStreamInfo);
			var streamMetadata = GetStreamMetadataUncached(reader, streamId);

			// Conditional update depending on previously returned cache info version.
			// If version is not correct -- nothing is changed in cache.
			// This update is conditioned to not interfere with updating stream cache info by commit procedure
			// (which is the source of truth).
			var res = _backend.UpdateStreamMetadata(cache.Version, streamId, streamMetadata);
			return res ?? streamMetadata;
		}

		private StreamMetadata GetStreamMetadataUncached(TFReaderLease reader, TStreamId streamId) {
			var metastreamId = _systemStreams.MetaStreamOf(streamId);
			var metaEventNumber = GetStreamLastEventNumberCached(reader, metastreamId);
			if (metaEventNumber == ExpectedVersion.NoStream || metaEventNumber == EventNumber.DeletedStream)
				return StreamMetadata.Empty;

			IPrepareLogRecord prepare = ReadPrepareInternal(reader, metastreamId, metaEventNumber);
			if (prepare == null)
				throw new Exception(string.Format(
					"ReadPrepareInternal could not find metaevent #{0} on metastream '{1}'. "
					+ "That should never happen.", metaEventNumber, metastreamId));

			if (prepare.Data.Length == 0 || prepare.Flags.HasNoneOf(PrepareFlags.IsJson))
				return StreamMetadata.Empty;

			try {
				var metadata = StreamMetadata.FromJsonBytes(prepare.Data);
				if (prepare.Version == LogRecordVersion.LogRecordV0 && metadata.TruncateBefore == int.MaxValue) {
					metadata = new StreamMetadata(metadata.MaxCount, metadata.MaxAge, EventNumber.DeletedStream,
						metadata.TempStream, metadata.CacheControl, metadata.Acl);
				}

				return metadata;
			} catch (Exception) {
				return StreamMetadata.Empty;
			}
		}
	}
}
