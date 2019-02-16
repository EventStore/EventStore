using System;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Common.Log;
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IIndexReader {
		long CachedStreamInfo { get; }
		long NotCachedStreamInfo { get; }
		long HashCollisions { get; }

		IndexReadEventResult ReadEvent(string streamId, long eventNumber);
		IndexReadStreamResult ReadStreamEventsForward(string streamId, long fromEventNumber, int maxCount);
		IndexReadStreamResult ReadStreamEventsBackward(string streamId, long fromEventNumber, int maxCount);

		/// <summary>
		/// Doesn't filter $maxAge, $maxCount, $tb(truncate before), doesn't check stream deletion, etc.
		/// </summary>
		PrepareLogRecord ReadPrepare(string streamId, long eventNumber);

		string GetEventStreamIdByTransactionId(long transactionId);
		StreamAccess CheckStreamAccess(string streamId, StreamAccessType streamAccessType, IPrincipal user);

		StreamMetadata GetStreamMetadata(string streamId);
		long GetStreamLastEventNumber(string streamId);
	}

	public class IndexReader : IIndexReader {
		private static readonly ILogger Log = LogManager.GetLoggerFor<IndexReader>();

		public long CachedStreamInfo {
			get { return Interlocked.Read(ref _cachedStreamInfo); }
		}

		public long NotCachedStreamInfo {
			get { return Interlocked.Read(ref _notCachedStreamInfo); }
		}

		public long HashCollisions {
			get { return Interlocked.Read(ref _hashCollisions); }
		}

		private readonly IIndexBackend _backend;
		private readonly ITableIndex _tableIndex;
		private readonly bool _skipIndexScanOnRead;
		private readonly StreamMetadata _metastreamMetadata;

		private long _hashCollisions;
		private long _cachedStreamInfo;
		private long _notCachedStreamInfo;
		private int _hashCollisionReadLimit;

		public IndexReader(IIndexBackend backend, ITableIndex tableIndex, StreamMetadata metastreamMetadata,
			int hashCollisionReadLimit, bool skipIndexScanOnRead) {
			Ensure.NotNull(backend, "backend");
			Ensure.NotNull(tableIndex, "tableIndex");
			Ensure.NotNull(metastreamMetadata, "metastreamMetadata");

			_backend = backend;
			_tableIndex = tableIndex;
			_metastreamMetadata = metastreamMetadata;
			_hashCollisionReadLimit = hashCollisionReadLimit;
			_skipIndexScanOnRead = skipIndexScanOnRead;
		}

		IndexReadEventResult IIndexReader.ReadEvent(string streamId, long eventNumber) {
			Ensure.NotNullOrEmpty(streamId, "streamId");
			if (eventNumber < -1) throw new ArgumentOutOfRangeException("eventNumber");
			using (var reader = _backend.BorrowReader()) {
				return ReadEventInternal(reader, streamId, eventNumber);
			}
		}

		private IndexReadEventResult ReadEventInternal(TFReaderLease reader, string streamId, long eventNumber) {
			var lastEventNumber = GetStreamLastEventNumberCached(reader, streamId);
			var metadata = GetStreamMetadataCached(reader, streamId);
			var originalStreamExists = OriginalStreamExists(reader, streamId);
			if (lastEventNumber == EventNumber.DeletedStream)
				return new IndexReadEventResult(ReadEventResult.StreamDeleted, metadata, lastEventNumber,
					originalStreamExists);
			if (lastEventNumber == ExpectedVersion.NoStream || metadata.TruncateBefore == EventNumber.DeletedStream)
				return new IndexReadEventResult(ReadEventResult.NoStream, metadata, lastEventNumber,
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

			if (eventNumber < minEventNumber || eventNumber > lastEventNumber)
				return new IndexReadEventResult(ReadEventResult.NotFound, metadata, lastEventNumber,
					originalStreamExists);

			PrepareLogRecord prepare = ReadPrepareInternal(reader, streamId, eventNumber);
			if (prepare != null) {
				if (metadata.MaxAge.HasValue && prepare.TimeStamp < DateTime.UtcNow - metadata.MaxAge.Value)
					return new IndexReadEventResult(ReadEventResult.NotFound, metadata, lastEventNumber,
						originalStreamExists);
				return new IndexReadEventResult(ReadEventResult.Success, new EventRecord(eventNumber, prepare),
					metadata, lastEventNumber, originalStreamExists);
			}

			return new IndexReadEventResult(ReadEventResult.NotFound, metadata, lastEventNumber,
				originalStreamExists: originalStreamExists);
		}

		PrepareLogRecord IIndexReader.ReadPrepare(string streamId, long eventNumber) {
			using (var reader = _backend.BorrowReader()) {
				return ReadPrepareInternal(reader, streamId, eventNumber);
			}
		}

		private PrepareLogRecord ReadPrepareInternal(TFReaderLease reader, string streamId, long eventNumber) {
			// we assume that you already did check for stream deletion
			Ensure.NotNullOrEmpty(streamId, "streamId");
			Ensure.Nonnegative(eventNumber, "eventNumber");

			return _skipIndexScanOnRead
				? ReadPrepareSkipScan(reader, streamId, eventNumber)
				: ReadPrepare(reader, streamId, eventNumber);
		}

		private PrepareLogRecord ReadPrepare(TFReaderLease reader, string streamId, long eventNumber) {
			var recordsQuery = _tableIndex.GetRange(streamId, eventNumber, eventNumber)
				.Select(x => new {x.Version, Prepare = ReadPrepareInternal(reader, x.Position)})
				.Where(x => x.Prepare != null && x.Prepare.EventStreamId == streamId)
				.GroupBy(x => x.Version).Select(x => x.Last()).ToList();
			if (recordsQuery.Count() == 1) {
				return recordsQuery.First().Prepare;
			}

			return null;
		}

		private PrepareLogRecord ReadPrepareSkipScan(TFReaderLease reader, string streamId, long eventNumber) {
			long position;
			if (_tableIndex.TryGetOneValue(streamId, eventNumber, out position)) {
				var rec = ReadPrepareInternal(reader, position);
				if (rec != null && rec.EventStreamId == streamId)
					return rec;

				foreach (var indexEntry in _tableIndex.GetRange(streamId, eventNumber, eventNumber)) {
					Interlocked.Increment(ref _hashCollisions);
					if (indexEntry.Position == position)
						continue;
					rec = ReadPrepareInternal(reader, indexEntry.Position);
					if (rec != null && rec.EventStreamId == streamId)
						return rec;
				}
			}

			return null;
		}

		protected static PrepareLogRecord ReadPrepareInternal(TFReaderLease reader, long logPosition) {
			RecordReadResult result = reader.TryReadAt(logPosition);
			if (!result.Success)
				return null;
			if (result.LogRecord.RecordType != LogRecordType.Prepare)
				throw new Exception(string.Format("Incorrect type of log record {0}, expected Prepare record.",
					result.LogRecord.RecordType));
			return (PrepareLogRecord)result.LogRecord;
		}

		IndexReadStreamResult IIndexReader.
			ReadStreamEventsForward(string streamId, long fromEventNumber, int maxCount) {
			return ReadStreamEventsForwardInternal(streamId, fromEventNumber, maxCount, _skipIndexScanOnRead);
		}

		private IndexReadStreamResult ReadStreamEventsForwardInternal(string streamId, long fromEventNumber,
			int maxCount, bool skipIndexScanOnRead) {
			Ensure.NotNullOrEmpty(streamId, "streamId");
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
					.Where(x => x.Prepare != null && x.Prepare.EventStreamId == streamId);
				if (!skipIndexScanOnRead) {
					recordsQuery = recordsQuery.OrderByDescending(x => x.Version)
						.GroupBy(x => x.Version).Select(x => x.Last());
				}

				if (metadata.MaxAge.HasValue) {
					var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
					recordsQuery = recordsQuery.Where(x => x.Prepare.TimeStamp >= ageThreshold);
				}

				var records = recordsQuery.Reverse().Select(x => new EventRecord(x.Version, x.Prepare)).ToArray();

				long nextEventNumber = Math.Min(endEventNumber + 1, lastEventNumber + 1);
				if (records.Length > 0)
					nextEventNumber = records[records.Length - 1].EventNumber + 1;
				var isEndOfStream = endEventNumber >= lastEventNumber;
				return new IndexReadStreamResult(endEventNumber, maxCount, records, metadata,
					nextEventNumber, lastEventNumber, isEndOfStream);
			}
		}

		IndexReadStreamResult IIndexReader.
			ReadStreamEventsBackward(string streamId, long fromEventNumber, int maxCount) {
			return ReadStreamEventsBackwardInternal(streamId, fromEventNumber, maxCount, _skipIndexScanOnRead);
		}

		private IndexReadStreamResult ReadStreamEventsBackwardInternal(string streamId, long fromEventNumber,
			int maxCount, bool skipIndexScanOnRead) {
			Ensure.NotNullOrEmpty(streamId, "streamId");
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
					.Where(x => x.Prepare != null && x.Prepare.EventStreamId == streamId);
				if (!skipIndexScanOnRead) {
					recordsQuery = recordsQuery.OrderByDescending(x => x.Version)
						.GroupBy(x => x.Version).Select(x => x.Last());
					;
				}

				if (metadata.MaxAge.HasValue) {
					var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
					recordsQuery = recordsQuery.Where(x => x.Prepare.TimeStamp >= ageThreshold);
				}

				var records = recordsQuery.Select(x => new EventRecord(x.Version, x.Prepare)).ToArray();

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

		public string GetEventStreamIdByTransactionId(long transactionId) {
			Ensure.Nonnegative(transactionId, "transactionId");
			using (var reader = _backend.BorrowReader()) {
				var res = ReadPrepareInternal(reader, transactionId);
				return res == null ? null : res.EventStreamId;
			}
		}


		StreamAccess IIndexReader.
			CheckStreamAccess(string streamId, StreamAccessType streamAccessType, IPrincipal user) {
			Ensure.NotNullOrEmpty(streamId, "streamId");
			using (var reader = _backend.BorrowReader()) {
				return CheckStreamAccessInternal(reader, streamId, streamAccessType, user);
			}
		}

		private StreamAccess CheckStreamAccessInternal(TFReaderLease reader, string streamId,
			StreamAccessType streamAccessType, IPrincipal user) {
			if (SystemStreams.IsMetastream(streamId)) {
				switch (streamAccessType) {
					case StreamAccessType.Read:
						return CheckStreamAccessInternal(reader, SystemStreams.OriginalStreamOf(streamId),
							StreamAccessType.MetaRead, user);
					case StreamAccessType.Write:
						return CheckStreamAccessInternal(reader, SystemStreams.OriginalStreamOf(streamId),
							StreamAccessType.MetaWrite, user);
					case StreamAccessType.Delete:
					case StreamAccessType.MetaRead:
					case StreamAccessType.MetaWrite:
						return new StreamAccess(false);
					default:
						throw new ArgumentOutOfRangeException("streamAccessType");
				}
			}

			if ((streamAccessType == StreamAccessType.Write || streamAccessType == StreamAccessType.Delete)
			    && streamId == SystemStreams.AllStream)
				return new StreamAccess(false);

			var sysSettings = _backend.GetSystemSettings() ?? SystemSettings.Default;
			var meta = GetStreamMetadataCached(reader, streamId);
			StreamAcl acl;
			StreamAcl sysAcl;
			StreamAcl defAcl;
			if (SystemStreams.IsSystemStream(streamId)) {
				defAcl = SystemSettings.Default.SystemStreamAcl;
				sysAcl = sysSettings.SystemStreamAcl ?? defAcl;
				acl = meta.Acl ?? sysAcl;
			} else {
				defAcl = SystemSettings.Default.UserStreamAcl;
				sysAcl = sysSettings.UserStreamAcl ?? defAcl;
				acl = meta.Acl ?? sysAcl;
			}

			string[] roles;
			switch (streamAccessType) {
				case StreamAccessType.Read:
					roles = acl.ReadRoles ?? sysAcl.ReadRoles ?? defAcl.ReadRoles;
					break;
				case StreamAccessType.Write:
					roles = acl.WriteRoles ?? sysAcl.WriteRoles ?? defAcl.WriteRoles;
					break;
				case StreamAccessType.Delete:
					roles = acl.DeleteRoles ?? sysAcl.DeleteRoles ?? defAcl.DeleteRoles;
					break;
				case StreamAccessType.MetaRead:
					roles = acl.MetaReadRoles ?? sysAcl.MetaReadRoles ?? defAcl.MetaReadRoles;
					break;
				case StreamAccessType.MetaWrite:
					roles = acl.MetaWriteRoles ?? sysAcl.MetaWriteRoles ?? defAcl.MetaWriteRoles;
					break;
				default: throw new ArgumentOutOfRangeException("streamAccessType");
			}

			var isPublic = roles.Contains(x => x == SystemRoles.All);
			if (isPublic) return new StreamAccess(true, true);
			if (user == null) return new StreamAccess(false);
			if (user.IsInRole(SystemRoles.Admins)) return new StreamAccess(true);
			for (int i = 0; i < roles.Length; ++i) {
				if (user.IsInRole(roles[i]))
					return new StreamAccess(true);
			}

			return new StreamAccess(false);
		}

		long IIndexReader.GetStreamLastEventNumber(string streamId) {
			Ensure.NotNullOrEmpty(streamId, "streamId");
			using (var reader = _backend.BorrowReader()) {
				return GetStreamLastEventNumberCached(reader, streamId);
			}
		}

		StreamMetadata IIndexReader.GetStreamMetadata(string streamId) {
			Ensure.NotNullOrEmpty(streamId, "streamId");
			using (var reader = _backend.BorrowReader()) {
				return GetStreamMetadataCached(reader, streamId);
			}
		}

		private long GetStreamLastEventNumberCached(TFReaderLease reader, string streamId) {
			// if this is metastream -- check if original stream was deleted, if yes -- metastream is deleted as well
			if (SystemStreams.IsMetastream(streamId)
			    && GetStreamLastEventNumberCached(reader, SystemStreams.OriginalStreamOf(streamId)) ==
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

		private long GetStreamLastEventNumberUncached(TFReaderLease reader, string streamId) {
			IndexEntry latestEntry;
			if (!_tableIndex.TryGetLatestEntry(streamId, out latestEntry))
				return ExpectedVersion.NoStream;

			var rec = ReadPrepareInternal(reader, latestEntry.Position);
			if (rec == null) throw new Exception("Could not read latest stream's prepare. That should never happen.");

			int count = 0;
			long startVersion = 0;
			long latestVersion = long.MinValue;
			if (rec.EventStreamId == streamId) {
				startVersion = Math.Max(latestEntry.Version, latestEntry.Version + 1);
				latestVersion = latestEntry.Version;
			}

			foreach (var indexEntry in _tableIndex.GetRange(streamId, startVersion, long.MaxValue,
				limit: _hashCollisionReadLimit + 1)) {
				var r = ReadPrepareInternal(reader, indexEntry.Position);
				if (r != null && r.EventStreamId == streamId) {
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

		private bool OriginalStreamExists(TFReaderLease reader, string metaStreamId) {
			if (SystemStreams.IsSystemStream(metaStreamId)) {
				var originalStreamId = SystemStreams.OriginalStreamOf(metaStreamId);
				var lastEventNumber = GetStreamLastEventNumberCached(reader, originalStreamId);
				if (lastEventNumber == ExpectedVersion.NoStream || lastEventNumber == EventNumber.DeletedStream)
					return false;
				return true;
			}

			return false;
		}

		private StreamMetadata GetStreamMetadataCached(TFReaderLease reader, string streamId) {
			// if this is metastream -- check if original stream was deleted, if yes -- metastream is deleted as well
			if (SystemStreams.IsMetastream(streamId))
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

		private StreamMetadata GetStreamMetadataUncached(TFReaderLease reader, string streamId) {
			var metastreamId = SystemStreams.MetastreamOf(streamId);
			var metaEventNumber = GetStreamLastEventNumberCached(reader, metastreamId);
			if (metaEventNumber == ExpectedVersion.NoStream || metaEventNumber == EventNumber.DeletedStream)
				return StreamMetadata.Empty;

			PrepareLogRecord prepare = ReadPrepareInternal(reader, metastreamId, metaEventNumber);
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
