using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
		bool TryReadPrepare(TStreamId streamId, long eventNumber, out IPrepareLogRecord<TStreamId> prepare, out long startEventNumber, out long endEventNumber, out int eventIndex);

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
		private readonly IExistenceFilterReader<TStreamId> _streamExistenceFilter;
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
			IExistenceFilterReader<TStreamId> streamExistenceFilter,
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
			_streamExistenceFilter = streamExistenceFilter;
			_metastreamMetadata = metastreamMetadata;
			_hashCollisionReadLimit = hashCollisionReadLimit;
			_skipIndexScanOnRead = skipIndexScanOnRead;
		}

		IndexReadEventResult IIndexReader<TStreamId>.ReadEvent(string streamName, TStreamId streamId, long eventNumber) {
			Ensure.Valid(streamId, _validator);
			if (eventNumber < -1)
				throw new ArgumentOutOfRangeException("eventNumber");
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
			//TODO(clc): confirm this logic, it seems that reads less than min should be invaild rather than found
			if (eventNumber < minEventNumber || eventNumber > lastEventNumber)
				return new IndexReadEventResult(ReadEventResult.NotFound, metadata, lastEventNumber,
					originalStreamExists);

			var prepare = ReadPrepareInternal(reader, streamId, eventNumber, out _, out _, out var eventIndex);
			if (prepare != null) {
				if (metadata.MaxAge.HasValue && prepare.TimeStamp < DateTime.UtcNow - metadata.MaxAge.Value)
					return new IndexReadEventResult(ReadEventResult.NotFound, metadata, lastEventNumber,
						originalStreamExists);
				return new IndexReadEventResult(ReadEventResult.Success, new EventRecord(streamName, prepare, eventIndex),
					metadata, lastEventNumber, originalStreamExists);
			}

			return new IndexReadEventResult(ReadEventResult.NotFound, metadata, lastEventNumber,
				originalStreamExists: originalStreamExists);
		}

		bool IIndexReader<TStreamId>.TryReadPrepare(TStreamId streamId, long eventNumber, out IPrepareLogRecord<TStreamId> prepare,
			out long startEventNumber, out long endEventNumber, out int eventIndex) {
			prepare = ReadPrepare(streamId, eventNumber, out startEventNumber, out endEventNumber, out eventIndex);
			return prepare is not null;
		}

		private IPrepareLogRecord<TStreamId> ReadPrepare(TStreamId streamId, long eventNumber, out long startEventNumber, out long endEventNumber, out int eventIndex) {
			using (var reader = _backend.BorrowReader()) {
				return ReadPrepareInternal(reader, streamId, eventNumber, out startEventNumber, out endEventNumber, out eventIndex);
			}
		}

		private IPrepareLogRecord<TStreamId> ReadPrepareInternal(TFReaderLease reader, TStreamId streamId, long eventNumber,
			out long startEventNumber, out long endEventNumber, out int eventIndex) {
			// we assume that you already did check for stream deletion
			Ensure.Valid(streamId, _validator);
			Ensure.Nonnegative(eventNumber, "eventNumber");

			return _skipIndexScanOnRead
				? ReadPrepareSkipScan(reader, streamId, eventNumber, out startEventNumber, out endEventNumber, out eventIndex)
				: ReadPrepare(reader, streamId, eventNumber, out startEventNumber, out endEventNumber, out eventIndex);
		}

		private IPrepareLogRecord<TStreamId> ReadPrepare(TFReaderLease reader, TStreamId streamId, long eventNumber,
			out long startEventNumber, out long endEventNumber, out int eventIndex) {
			var recordsQuery = _tableIndex.GetRange(streamId, eventNumber, eventNumber)
				.Select(x => {
					var prepare = ReadPrepareInternal(reader, x.Position, x.Version, out var startEventNumber, out var endEventNumber);
					return new {x.Version, Prepare = prepare, StartEventNumber = startEventNumber,
						EndEventNumber = endEventNumber, EventIndex = (int) (eventNumber - startEventNumber)};
				})
				.Where(x => x.Prepare != null && StreamIdComparer.Equals(x.Prepare.EventStreamId, streamId))
				.GroupBy(x => x.Version).Select(x => x.Last()).ToList();
			if (recordsQuery.Count() == 1) {
				var record = recordsQuery.First();
				startEventNumber = record.StartEventNumber;
				endEventNumber = record.EndEventNumber;
				eventIndex = record.EventIndex;
				return record.Prepare;
			}

			startEventNumber = -1;
			endEventNumber = -1;
			eventIndex = -1;
			return null;
		}

		private IPrepareLogRecord<TStreamId> ReadPrepareSkipScan(TFReaderLease reader, TStreamId streamId, long eventNumber,
			out long startEventNumber, out long endEventNumber, out int eventIndex) {
			long position;
			if (_tableIndex.TryGetOneValue(streamId, eventNumber, out position)) {
				var rec = ReadPrepareInternal(reader, position, eventNumber, out startEventNumber, out endEventNumber);
				if (rec != null && StreamIdComparer.Equals(rec.EventStreamId, streamId)) {
					eventIndex = (int)(eventNumber - startEventNumber);
					return rec;
				}

				foreach (var indexEntry in _tableIndex.GetRange(streamId, eventNumber, eventNumber)) {
					Interlocked.Increment(ref _hashCollisions);
					if (indexEntry.Position == position)
						continue;
					rec = ReadPrepareInternal(reader, indexEntry.Position, indexEntry.Version, out startEventNumber, out endEventNumber);
					if (rec != null && StreamIdComparer.Equals(rec.EventStreamId, streamId)) {
						eventIndex = (int)(eventNumber - startEventNumber);
						return rec;
					}
				}
			}

			startEventNumber = -1;
			endEventNumber = -1;
			eventIndex = -1;
			return null;
		}

		private static void ReadPrepareIfDifferent(
			TStreamId streamId,
			long eventNumber,
			TFReaderLease reader,
			long eventLogPosition,
			IPrepareLogRecord<TStreamId> currentPrepare,
			out IPrepareLogRecord<TStreamId> prepare,
			out long startEventNumber,
			out long endEventNumber,
			out int eventIndex) {
			if (currentPrepare == null
			    || !StreamIdComparer.Equals(currentPrepare.EventStreamId, streamId)
			    || eventNumber < currentPrepare.ExpectedVersion + 1
			    || eventNumber > currentPrepare.ExpectedVersion + currentPrepare.Events.Length
			    || currentPrepare.Events[eventNumber - (currentPrepare.ExpectedVersion + 1)].LogPosition != eventLogPosition) {
				prepare = ReadPrepareInternal(reader, eventLogPosition, eventNumber, out startEventNumber, out endEventNumber);
			} else {
				prepare = currentPrepare;
				startEventNumber = currentPrepare.ExpectedVersion + 1;
				endEventNumber = currentPrepare.ExpectedVersion + currentPrepare.Events.Length;
			}

			if (prepare != null) {
				eventIndex = (int)(eventNumber - startEventNumber);
			} else {
				eventIndex = -1;
			}
		}

		protected static IPrepareLogRecord<TStreamId> ReadPrepareInternal(TFReaderLease reader, long logPosition,
			long? eventNumber, out long startEventNumber, out long endEventNumber) {
			RecordReadResult result = reader.TryReadAt(logPosition);
			if (!result.Success) {
				startEventNumber = -1;
				endEventNumber = -1;
				return null;
			}

			if (result.LogRecord.RecordType != LogRecordType.Prepare &&
			    result.LogRecord.RecordType != LogRecordType.Stream)
				throw new Exception(string.Format("Incorrect type of log record {0}, expected Prepare record.",
					result.LogRecord.RecordType));

			var prepare = (IPrepareLogRecord<TStreamId>)result.LogRecord;
			if (eventNumber.HasValue) {
				prepare.PopulateExpectedVersion(eventNumber.Value - 1);
			}

			startEventNumber = prepare.ExpectedVersion + 1;
			endEventNumber = prepare.ExpectedVersion + prepare.Events.Length;
			return prepare;
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
				if (metadata.MaxAge.HasValue) {
					return ForStreamWithMaxAge(streamId, streamName,
						fromEventNumber, maxCount,
						startEventNumber, endEventNumber, lastEventNumber,
						metadata.MaxAge.Value, metadata, _tableIndex, reader, skipIndexScanOnRead);
				}

				IEnumerable <(long EventNumber, IPrepareLogRecord<TStreamId> Prepare, int EventIndex)> recordsQuery
					= new List<(long EventNumber, IPrepareLogRecord<TStreamId> Prepare, int EventIndex)>();

				var indexEntries = _tableIndex.GetRange(streamId, startEventNumber, endEventNumber);

				IPrepareLogRecord<TStreamId> prepare = null;
				foreach(var indexEntry in indexEntries) {
					ReadPrepareIfDifferent(
						streamId,
						indexEntry.Version,
						reader,
						indexEntry.Position,
						prepare,
						out prepare,
						out _,
						out _,
						out var eventIndex);
					if (prepare != null) {
						((List<(long EventNumber, IPrepareLogRecord<TStreamId> Prepare, int EventIndex)>) recordsQuery)
							.Add((indexEntry.Version, prepare, eventIndex));
					}
				}

				if (!skipIndexScanOnRead) {
					recordsQuery = recordsQuery.OrderByDescending(x => x.EventNumber)
						.GroupBy(x => x.EventNumber).Select(x => x.Last());
				}

				var records = recordsQuery.Reverse().Select(x => new EventRecord(streamName, x.Prepare, x.EventIndex)).ToArray();

				long nextEventNumber = Math.Min(endEventNumber + 1, lastEventNumber + 1);
				if (records.Length > 0)
					nextEventNumber = records[records.Length - 1].EventNumber + 1;
				var isEndOfStream = endEventNumber >= lastEventNumber;
				return new IndexReadStreamResult(endEventNumber, maxCount, records, metadata,
					nextEventNumber, lastEventNumber, isEndOfStream);
			}

			static IndexReadStreamResult ForStreamWithMaxAge(TStreamId streamId,
				string streamName,
				long fromEventNumber, int maxCount, long startEventNumber,
				long endEventNumber, long lastEventNumber, TimeSpan maxAge, StreamMetadata metadata,
				ITableIndex<TStreamId> tableIndex, TFReaderLease reader, bool skipIndexScanOnRead) {
				if (startEventNumber > lastEventNumber) {
					return new IndexReadStreamResult(fromEventNumber, maxCount, IndexReadStreamResult.EmptyRecords,
						metadata, lastEventNumber + 1, lastEventNumber, isEndOfStream: true);
				}

				var ageThreshold = DateTime.UtcNow - maxAge;
				var nextEventNumber = lastEventNumber;
				var indexEntries = tableIndex.GetRange(streamId, startEventNumber, endEventNumber);

				//Move to the first valid entries. At this point we could instead return an empty result set with the minimum set, but that would 
				//involve an additional set of reads for no good reason
				while (indexEntries.Count == 0) {
					// this will generally only iterate once, unless a scavenge completes exactly now, in which case it might iterate twice
					if (tableIndex.TryGetOldestEntry(streamId, out var oldest)) {
						startEventNumber = oldest.Version;
						endEventNumber = startEventNumber + maxCount - 1;
						indexEntries = tableIndex.GetRange(streamId, startEventNumber, endEventNumber, maxCount);
					} else {
						//scavenge completed and deleted our stream? return empty set and get the client to try again?
						return new IndexReadStreamResult(fromEventNumber, maxCount, IndexReadStreamResult.EmptyRecords,
							metadata, lastEventNumber + 1, lastEventNumber, isEndOfStream: false);
					}
				}

				var results = new List<EventRecord>();
				IPrepareLogRecord<TStreamId> prepare = null;
				for (int i = 0; i < indexEntries.Count; i++) {
					ReadPrepareIfDifferent(
						streamId,
						indexEntries[i].Version,
						reader,
						indexEntries[i].Position,
						prepare,
						out prepare,
						out _,
						out _,
						out var eventIndex);

					//LOGV2
					if (typeof(TStreamId) == typeof(string) &&
					    (prepare == null || !StreamIdComparer.Equals(prepare.EventStreamId, streamId))) {
						continue;
					}

					if (prepare?.TimeStamp >= ageThreshold) {
						results.Add(new EventRecord(streamName, prepare, eventIndex));
					} else {
						break;
					}

				}

				if (results.Count > 0) {
					//We got at least one event in the correct age range, so we will return whatever was valid and indicate where to read from next
					nextEventNumber = results[0].EventNumber + 1;
					results.Reverse();

					var isEndOfStream = endEventNumber >= lastEventNumber;
					return new IndexReadStreamResult(endEventNumber, maxCount, results.ToArray(), metadata,
						nextEventNumber, lastEventNumber, isEndOfStream);
				}

				//we didn't find anything valid yet, now we need to search

				//check high value will be valid
				if (tableIndex.TryGetLatestEntry(streamId, out var latest)) {
					var end = ReadPrepareInternal(reader, latest.Position, latest.Version, out _, out _);
					if (end.TimeStamp < ageThreshold || latest.Version < fromEventNumber) {
						//No events in the stream are < max age, so return an empty set
						return new IndexReadStreamResult(fromEventNumber, maxCount, IndexReadStreamResult.EmptyRecords,
							metadata, latest.Version + end.Events.Length, latest.Version + end.Events.Length - 1, isEndOfStream: true);
					}
				} else {
					//For some reason there is no last event in this stream, maybe a scavenge completed and deleted the stream, send back for retry
					return new IndexReadStreamResult(fromEventNumber, maxCount, IndexReadStreamResult.EmptyRecords,
						metadata, lastEventNumber + 1, lastEventNumber, isEndOfStream: false);
				}


				
				var low = indexEntries[0].Version;
				var high = latest.Version;
				while (low <= high) {
					var mid = low + ((high - low) / 2);
					indexEntries = tableIndex.GetRange(streamId, mid, mid + maxCount, maxCount);
					if (indexEntries.Count > 0) {
						nextEventNumber = indexEntries[0].Version + 1;
					}

					var lowPrepare = LowPrepare(reader, indexEntries, streamId);
					if (lowPrepare?.TimeStamp >= ageThreshold) {
						high = mid - 1;
						nextEventNumber = lowPrepare.ExpectedVersion + lowPrepare.Events.Length;
						continue;
					}

					var highPrepare = HighPrepare(reader, indexEntries, streamId);
					if (highPrepare?.TimeStamp < ageThreshold) {
						low = mid + indexEntries.Count;
						continue;
					}

					//ok, some entries must match, if not (due to time moving forwards) we can just reissue based on the current mid
					prepare = null;
					for (int i = 0; i < indexEntries.Count; i++) {
						ReadPrepareIfDifferent(
							streamId,
							indexEntries[i].Version,
							reader,
							indexEntries[i].Position,
							prepare,
							out prepare,
							out _,
							out _,
							out var eventIndex);
						//LOGV2
						if (typeof(TStreamId) == typeof(string) &&
						    (prepare == null || !StreamIdComparer.Equals(prepare.EventStreamId, streamId))) {
							continue;
						}
						if (prepare?.TimeStamp >= ageThreshold) {
							results.Add(new EventRecord(streamName, prepare, eventIndex));
						} else {
							break;
						}
					}

					if (results.Count > 0) {
						//We got at least one event in the correct age range, so we will return whatever was valid and indicate where to read from next
						endEventNumber = results[0].EventNumber;
						nextEventNumber = endEventNumber + 1;
						results.Reverse();
						var isEndOfStream = endEventNumber >= lastEventNumber;

						var maxEventNumberToReturn = fromEventNumber + maxCount;
						while (results.Count > 0 && results[^1].EventNumber > maxEventNumberToReturn) {
							nextEventNumber = results[^1].EventNumber;
							results.Remove(results[^1]);
						}

						if (results.Count == 0) nextEventNumber--;
						return new IndexReadStreamResult(endEventNumber, maxCount, results.ToArray(), metadata,
							nextEventNumber, lastEventNumber, isEndOfStream);
					}

					break;
				}

				//We didn't find anything, send back to the client with the latest position to retry
				return new IndexReadStreamResult(fromEventNumber, maxCount, IndexReadStreamResult.EmptyRecords,
					metadata, nextEventNumber, lastEventNumber, isEndOfStream: false);
				[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
				static IPrepareLogRecord<TStreamId> LowPrepare(TFReaderLease tfReaderLease,
					IReadOnlyList<IndexEntry> entries, TStreamId streamId) {
					
					if (typeof(TStreamId) == typeof(string)) {
						for (int i = entries.Count - 1; i >= 0; i--) {
							var prepare = ReadPrepareInternal(tfReaderLease, entries[i].Position, entries[i].Version, out _, out _);
							if (prepare != null && StreamIdComparer.Equals(prepare.EventStreamId, streamId))
								return prepare;
						}

						return null;
					}

					for (int i = entries.Count - 1; i >= 0; i--) {
						var prepare = ReadPrepareInternal(tfReaderLease, entries[i].Position, entries[i].Version,  out _, out _);
						if (prepare != null) return prepare;
					}

					return null;
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
				static IPrepareLogRecord<TStreamId> HighPrepare(TFReaderLease tfReaderLease,
					IReadOnlyList<IndexEntry> entries, TStreamId streamId) {
					
					if (typeof(TStreamId) == typeof(string)) {
						for (int i = 0; i < entries.Count; i++) {
							var prepare = ReadPrepareInternal(tfReaderLease, entries[i].Position, entries[i].Version, out _, out _);
							if (prepare != null && StreamIdComparer.Equals(prepare.EventStreamId, streamId))
								return prepare;
						}

						return null;
					}

					for (int i = 0; i < entries.Count; i++) {
						var prepare = ReadPrepareInternal(tfReaderLease, entries[i].Position, entries[i].Version, out _, out _);
						if (prepare != null) return prepare;
					}

					return null;
				}
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

				IEnumerable <(long EventNumber, IPrepareLogRecord<TStreamId> Prepare, int EventIndex)> recordsQuery
					= new List<(long EventNumber, IPrepareLogRecord<TStreamId> Prepare, int EventIndex)>();

				var indexEntries = _tableIndex.GetRange(streamId, startEventNumber, endEventNumber);

				IPrepareLogRecord<TStreamId> prepare = null;
				foreach(var indexEntry in indexEntries) {
					ReadPrepareIfDifferent(
						streamId,
						indexEntry.Version,
						reader,
						indexEntry.Position,
						prepare,
						out prepare,
						out _,
						out _,
						out var eventIndex);
					if (prepare != null) {
						((List<(long EventNumber, IPrepareLogRecord<TStreamId> Prepare, int EventIndex)>) recordsQuery)
							.Add((indexEntry.Version, prepare, eventIndex));
					}
				}

				if (!skipIndexScanOnRead) {
					recordsQuery = recordsQuery.OrderByDescending(x => x.EventNumber)
						.GroupBy(x => x.EventNumber).Select(x => x.Last());
				}

				if (metadata.MaxAge.HasValue) {
					var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
					recordsQuery = recordsQuery.Where(x => x.Prepare.TimeStamp >= ageThreshold);
				}

				var records = recordsQuery.Select(x => new EventRecord(streamName, x.Prepare, x.EventIndex)).ToArray();

				isEndOfStream = isEndOfStream
								|| startEventNumber == 0
								|| (startEventNumber <= lastEventNumber
									&& (records.Length == 0 ||
										records[^1].EventNumber != startEventNumber));
				long nextEventNumber = isEndOfStream ? -1 : Math.Min(startEventNumber - 1, lastEventNumber);
				return new IndexReadStreamResult(endEventNumber, maxCount, records, metadata,
					nextEventNumber, lastEventNumber, isEndOfStream);
			}
		}

		public TStreamId GetEventStreamIdByTransactionId(long transactionId) {
			Ensure.Nonnegative(transactionId, "transactionId");
			using (var reader = _backend.BorrowReader()) {
				var res = ReadPrepareInternal(reader, transactionId, null, out _, out _);
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
			if (!_streamExistenceFilter.MightContain(streamId))
				return ExpectedVersion.NoStream;

			IndexEntry latestEntry;
			if (!_tableIndex.TryGetLatestEntry(streamId, out latestEntry))
				return ExpectedVersion.NoStream;

			var rec = ReadPrepareInternal(reader, latestEntry.Position, latestEntry.Version, out _, out _);
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

			IPrepareLogRecord<TStreamId> r = null;
			foreach (var indexEntry in _tableIndex.GetRange(streamId, startVersion, long.MaxValue,
				limit: _hashCollisionReadLimit + 1)) {
				ReadPrepareIfDifferent(streamId, indexEntry.Version, reader, indexEntry.Position,
					r, out r, out _, out _, out _);
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

		private bool? OriginalStreamExists(TFReaderLease reader, TStreamId metaStreamId) {
			if (_systemStreams.IsMetaStream(metaStreamId)) {
				var originalStreamId = _systemStreams.OriginalStreamOf(metaStreamId);
				var lastEventNumber = GetStreamLastEventNumberCached(reader, originalStreamId);
				if (lastEventNumber == ExpectedVersion.NoStream || lastEventNumber == EventNumber.DeletedStream)
					return false;
				return true;
			}

			return null;
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

			IPrepareLogRecord prepare = ReadPrepareInternal(reader, metastreamId, metaEventNumber,
				out _, out _, out var eventIndex);
			if (prepare == null)
				throw new Exception(string.Format(
					"ReadPrepareInternal could not find metaevent #{0} on metastream '{1}'. "
					+ "That should never happen.", metaEventNumber, metastreamId));

			if (prepare.Events[eventIndex].Data.Length == 0 || (prepare.Events[eventIndex].EventFlags & EventFlags.IsJson) == 0)
				return StreamMetadata.Empty;

			try {
				var metadata = StreamMetadata.FromJsonBytes(prepare.Events[eventIndex].Data);
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
