using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.DataStructures.ProbabilisticFilter;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.LogRecords;
using Serilog;
using Range = EventStore.Core.Data.Range;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	public partial class TFChunk {
		public interface IChunkReadSide {
			void RequestCaching();
			void Uncache();

			bool ExistsAt(long logicalPosition, ITransactionFileTracker tracker);
			long GetActualPosition(long logicalPosition, ITransactionFileTracker tracker);
			RecordReadResult TryReadAt(long logicalPosition, bool couldBeScavenged, ITransactionFileTracker tracker);
			RecordReadResult TryReadFirst(ITransactionFileTracker tracker);
			RecordReadResult TryReadClosestForward(long logicalPosition, ITransactionFileTracker tracker);
			RawReadResult TryReadClosestForwardRaw(long logicalPosition, Func<int, byte[]> getBuffer, ITransactionFileTracker tracker);
			RecordReadResult TryReadLast(ITransactionFileTracker tracker);
			RecordReadResult TryReadClosestBackward(long logicalPosition, ITransactionFileTracker tracker);
		}

		private class TFChunkReadSideUnscavenged : TFChunkReadSide, IChunkReadSide {
			public TFChunkReadSideUnscavenged(TFChunk chunk) : base(chunk) {
				if (chunk.ChunkHeader.IsScavenged)
					throw new ArgumentException("Scavenged TFChunk passed into unscavenged chunk read side.");
			}

			public void RequestCaching() {
				// do nothing
			}

			public void Uncache() {
				// do nothing
			}

			public bool ExistsAt(long logicalPosition, ITransactionFileTracker tracker) {
				return logicalPosition >= 0 && logicalPosition < Chunk.LogicalDataSize;
			}

			public long GetActualPosition(long logicalPosition, ITransactionFileTracker tracker) {
				Ensure.Nonnegative(logicalPosition, nameof(logicalPosition));

				if (logicalPosition >= Chunk.LogicalDataSize)
					return -1;

				return logicalPosition;
			}

			public RecordReadResult TryReadAt(long logicalPosition, bool couldBeScavenged, ITransactionFileTracker tracker) {
				var workItem = Chunk.GetReaderWorkItem(tracker);
				try {
					if (logicalPosition >= Chunk.LogicalDataSize) {
						_log.Warning(
							"Tried to read logical position {logicalPosition} which is greater than the chunk's logical size of {chunkLogicalSize}",
							logicalPosition, Chunk.LogicalDataSize);
						return RecordReadResult.Failure;
					}

					ILogRecord record;
					int length;
					var result = TryReadForwardInternal(workItem, logicalPosition, out length, out record);
					return new RecordReadResult(result, -1, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			public RecordReadResult TryReadFirst(ITransactionFileTracker tracker) {
				return TryReadClosestForward(0, tracker);
			}

			public RecordReadResult TryReadClosestForward(long logicalPosition, ITransactionFileTracker tracker) {
				var workItem = Chunk.GetReaderWorkItem(tracker);
				try {
					if (logicalPosition >= Chunk.LogicalDataSize)
						return RecordReadResult.Failure;

					if (!TryReadForwardInternal(workItem, logicalPosition, out var length, out var record))
						return RecordReadResult.Failure;

					long nextLogicalPos = record.GetNextLogPosition(logicalPosition, length);
					return new RecordReadResult(true, nextLogicalPos, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			public RawReadResult TryReadClosestForwardRaw(long logicalPosition, Func<int, byte[]> getBuffer, ITransactionFileTracker tracker) {
				var workItem = Chunk.GetReaderWorkItem(tracker);
				try {
					if (logicalPosition >= Chunk.LogicalDataSize)
						return RawReadResult.Failure;

					if (!TryReadForwardRawInternal(workItem, logicalPosition, getBuffer, out var length, out var record))
						return RawReadResult.Failure;

					var nextLogicalPos = logicalPosition + length + 2 * sizeof(int);
					return new RawReadResult(true, nextLogicalPos, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			public RecordReadResult TryReadLast(ITransactionFileTracker tracker) {
				return TryReadClosestBackward(Chunk.LogicalDataSize, tracker);
			}

			public RecordReadResult TryReadClosestBackward(long logicalPosition, ITransactionFileTracker tracker) {
				var workItem = Chunk.GetReaderWorkItem(tracker);
				try {
					// here we allow actualPosition == _logicalDataSize as we can read backward the very last record that way
					if (logicalPosition > Chunk.LogicalDataSize)
						return RecordReadResult.Failure;

					int length;
					ILogRecord record;
					if (!TryReadBackwardInternal(workItem, logicalPosition, out length, out record))
						return RecordReadResult.Failure;

					long nextLogicalPos = record.GetPrevLogPosition(logicalPosition, length);
					return new RecordReadResult(true, nextLogicalPos, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}
		}

		private class TFChunkReadSideScavenged : TFChunkReadSide, IChunkReadSide {
			// must hold _lock to assign to _wantMidpoints and _midpoints
			private readonly object _lock = new();
			private bool _wantMidpoints;
			private Midpoint[] _midpoints;
			private bool _optimizeCache;
			private InMemoryBloomFilter _logPositionsBloomFilter;

			private bool CacheIsOptimized {
				get { return _optimizeCache && _logPositionsBloomFilter != null; }
			}

			public TFChunkReadSideScavenged(TFChunk chunk, bool optimizeCache)
				: base(chunk) {
				_optimizeCache = optimizeCache;
				if (!chunk.ChunkHeader.IsScavenged)
					throw new ArgumentException(string.Format("Chunk provided is not scavenged: {0}", chunk));
			}

			public void Uncache() {
				lock (_lock) {
					_wantMidpoints = false;
					_midpoints = null;
				}
			}

			public void RequestCaching() {
				lock (_lock) {
					_wantMidpoints = true;
				}
			}

			private Midpoint[] GetOrCreateMidPoints(ReaderWorkItem workItem) {
				// don't use mipoints when reading from memory
				if (workItem.IsMemory)
					return null;

				// if we have midpoints we are happy. no synchronization required.
				// this value may be stale but the midpoints are still valid
				var midpoints = _midpoints;
				if (midpoints != null)
					return midpoints;

				// if we don't want midpoints we are happy. no synchronization required.
				// this value may be stale but this is rare and worst case we will perform the read
				// without the midpoints which will still work.
				if (!_wantMidpoints)
					return null;

				lock (_lock) {
					// guaranteed up to date. we don't want to assign to _midpoints if we aren't supposed to
					// because the midpoints will take up memory unnecessarily.
					if (!_wantMidpoints)
						return null;

					// want midpoints but don't have them, get them. synchronization is ok here because rare
					_midpoints = PopulateMidpoints(Chunk.MidpointsDepth, workItem);
					return _midpoints;
				}
			}

			public void OptimizeExistsAt(ITransactionFileTracker tracker) {
				if (_optimizeCache && _logPositionsBloomFilter == null)
					_logPositionsBloomFilter = PopulateBloomFilter(tracker);
			}

			public void DeOptimizeExistsAt() {
				if (_logPositionsBloomFilter != null)
					_logPositionsBloomFilter = null;
			}

			private InMemoryBloomFilter PopulateBloomFilter(ITransactionFileTracker tracker) {
				var mapCount = Chunk.ChunkFooter.MapCount;
				if (mapCount <= 0)
					return null;

				InMemoryBloomFilter bf = null;
				double p = 1e-4; //false positive probability

				while (p < 1.0) {
					try {
						bf = new InMemoryBloomFilter(mapCount, p);
						//Log.Debug("Created bloom filter with {numBits} bits and {numHashFunctions} hash functions for chunk {chunk} with map count: {mapCount}", bf.NumBits, bf.NumHashFunctions, Chunk.FileName, mapCount);
						break;
					} catch (ArgumentOutOfRangeException) {
						p *= 10.0;
					}
				}

				if (bf == null) {
					Log.Warning("Could not create bloom filter for chunk: {chunk}, map count: {mapCount}", Chunk.FileName,
						mapCount);
					return null;
				}

				ReaderWorkItem workItem = null;
				try {
					workItem = Chunk.GetReaderWorkItem(tracker);

					foreach (var posMap in ReadPosMap(workItem, 0, mapCount)) {
						bf.Add(posMap.LogPos);
					}

					//Log.Debug("{mapCount} items added to bloom filter for chunk {chunk}", mapCount, Chunk.FileName);
					return bf;
				} catch (FileBeingDeletedException) {
					return null;
				} catch (OutOfMemoryException) {
					return null;
				} finally {
					if (workItem != null)
						Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			private Midpoint[] PopulateMidpoints(int depth, ReaderWorkItem workItem) {
				if (depth > 31)
					throw new ArgumentOutOfRangeException("depth", "Depth too for midpoints.");

				if (Chunk.ChunkFooter.MapCount == 0) // empty chunk
					return null;

				try {
					int midPointsCnt = 1 << depth;
					int segmentSize;
					Midpoint[] midpoints;
					var mapCount = Chunk.ChunkFooter.MapCount;
					if (mapCount < midPointsCnt) {
						segmentSize = 1; // we cache all items
						midpoints = new Midpoint[mapCount];
					} else {
						segmentSize = mapCount / midPointsCnt;
						midpoints = new Midpoint[1 + (mapCount + segmentSize - 1) / segmentSize];
					}

					for (int x = 0, i = 0, xN = mapCount - 1; x < xN; x += segmentSize, i += 1) {
						midpoints[i] = new Midpoint(x, ReadPosMap(workItem, x));
					}

					// add the very last item as the last midpoint (possibly it is done twice)
					midpoints[midpoints.Length - 1] = new Midpoint(mapCount - 1, ReadPosMap(workItem, mapCount - 1));
					return midpoints;
				} catch (FileBeingDeletedException) {
					return null;
				} catch (OutOfMemoryException) {
					return null;
				}
			}

			private PosMap ReadPosMap(ReaderWorkItem workItem, long index) {
				if (Chunk.ChunkFooter.IsMap12Bytes) {
					var pos = ChunkHeader.Size + Chunk.ChunkFooter.PhysicalDataSize + index * PosMap.FullSize;
					workItem.Stream.Seek(pos, SeekOrigin.Begin);
					return PosMap.FromNewFormat(workItem.Reader);
				} else {
					var pos = ChunkHeader.Size + Chunk.ChunkFooter.PhysicalDataSize + index * PosMap.DeprecatedSize;
					workItem.Stream.Seek(pos, SeekOrigin.Begin);
					return PosMap.FromOldFormat(workItem.Reader);
				}
			}

			private IEnumerable<PosMap> ReadPosMap(ReaderWorkItem workItem, long index, int count) {
				if (Chunk.ChunkFooter.IsMap12Bytes) {
					var pos = ChunkHeader.Size + Chunk.ChunkFooter.PhysicalDataSize + index * PosMap.FullSize;
					workItem.Stream.Seek(pos, SeekOrigin.Begin);
					for (int i = 0; i < count; i++)
						yield return PosMap.FromNewFormat(workItem.Reader);
				} else {
					var pos = ChunkHeader.Size + Chunk.ChunkFooter.PhysicalDataSize + index * PosMap.DeprecatedSize;
					workItem.Stream.Seek(pos, SeekOrigin.Begin);
					for (int i = 0; i < count; i++)
						yield return PosMap.FromOldFormat(workItem.Reader);
				}
			}

			public bool ExistsAt(long logicalPosition, ITransactionFileTracker tracker) {
				if (CacheIsOptimized)
					return MayExistAt(logicalPosition);

				var workItem = Chunk.GetReaderWorkItem(tracker);
				try {
					var actualPosition = TranslateExactPosition(workItem, logicalPosition);
					return actualPosition >= 0 && actualPosition < Chunk.PhysicalDataSize;
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			public bool MayExistAt(long logicalPosition) {
				/* This function is much faster than ExistsAt. However, it may return false positives (with a very low probability) but never false negatives */
				return _logPositionsBloomFilter.MightContain(logicalPosition);
			}

			public long GetActualPosition(long logicalPosition, ITransactionFileTracker tracker) {
				Ensure.Nonnegative(logicalPosition, nameof(logicalPosition));

				var workItem = Chunk.GetReaderWorkItem(tracker);
				try {
					return TranslateExactPosition(workItem, logicalPosition);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			public RecordReadResult TryReadAt(long logicalPosition, bool couldBeScavenged, ITransactionFileTracker tracker) {
				var workItem = Chunk.GetReaderWorkItem(tracker);
				try {
					var actualPosition = TranslateExactPosition(workItem, logicalPosition);
					if (actualPosition == -1 || actualPosition >= Chunk.PhysicalDataSize) {
						if (!couldBeScavenged) {
							_log.Warning(
								"Tried to read actual position {actualPosition}, translated from logPosition {logicalPosition}, " +
								"which is greater than the chunk's physical size of {chunkPhysicalSize}",
								actualPosition, logicalPosition, Chunk.PhysicalDataSize);
						}
						return RecordReadResult.Failure;
					}

					ILogRecord record;
					int length;
					var result = TryReadForwardInternal(workItem, actualPosition, out length, out record);
					return new RecordReadResult(result, -1, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			private int TranslateExactPosition(ReaderWorkItem workItem, long pos) {
				var midpoints = GetOrCreateMidPoints(workItem);
				if (midpoints == null)
					return TranslateExactWithoutMidpoints(workItem, pos, 0, Chunk.ChunkFooter.MapCount - 1);
				return TranslateExactWithMidpoints(workItem, midpoints, pos);
			}

			private int TranslateExactWithoutMidpoints(ReaderWorkItem workItem, long pos, long startIndex,
				long endIndex) {
				long low = startIndex;
				long high = endIndex;
				while (low <= high) {
					var mid = low + (high - low) / 2;
					var v = ReadPosMap(workItem, mid);

					if (v.LogPos == pos)
						return v.ActualPos;
					if (v.LogPos < pos)
						low = mid + 1;
					else
						high = mid - 1;
				}

				return -1;
			}

			private int TranslateExactWithMidpoints(ReaderWorkItem workItem, Midpoint[] midpoints, long pos) {
				if (pos < midpoints[0].LogPos || pos > midpoints[midpoints.Length - 1].LogPos)
					return -1;

				var recordRange = LocatePosRange(midpoints, pos);
				return TranslateExactWithoutMidpoints(workItem, pos, recordRange.Lower, recordRange.Upper);
			}

			public RecordReadResult TryReadFirst(ITransactionFileTracker tracker) {
				return TryReadClosestForward(0, tracker);
			}

			public RecordReadResult TryReadClosestForward(long logicalPosition, ITransactionFileTracker tracker) {
				if (Chunk.ChunkFooter.MapCount == 0)
					return RecordReadResult.Failure;

				var workItem = Chunk.GetReaderWorkItem(tracker);
				try {
					var actualPosition = TranslateClosestForwardPosition(workItem, logicalPosition);
					if (actualPosition == -1 || actualPosition >= Chunk.PhysicalDataSize)
						return RecordReadResult.Failure;

					if (!TryReadForwardInternal(workItem, actualPosition, out var length, out var record))
						return RecordReadResult.Failure;

					long nextLogicalPos =
						Chunk.ChunkHeader.GetLocalLogPosition(record.GetNextLogPosition(record.LogPosition, length));
					return new RecordReadResult(true, nextLogicalPos, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			public RawReadResult TryReadClosestForwardRaw(long logicalPosition, Func<int, byte[]> getBuffer, ITransactionFileTracker tracker) {
				if (Chunk.ChunkFooter.MapCount == 0)
					return RawReadResult.Failure;

				var workItem = Chunk.GetReaderWorkItem(tracker);
				try {
					var actualPosition = TranslateClosestForwardPosition(workItem, logicalPosition);
					if (actualPosition == -1 || actualPosition >= Chunk.PhysicalDataSize)
						return RawReadResult.Failure;

					if (!TryReadForwardRawInternal(workItem, actualPosition, getBuffer, out var length, out var record))
						return RawReadResult.Failure;

					// We need to read the record's log position from the buffer so that we can correctly compute
					// the next position. Simply adding the record's length, suffix & prefix to "logicalPosition" won't
					// work properly with scavenged chunks since the computed position may still be before the current
					// record's position, which would cause us to read it again.
					if (!BitConverter.IsLittleEndian)
						throw new NotSupportedException();

					const int logPositionOffset = 2;
					var recordLogPos = BitConverter.ToInt64(record, logPositionOffset);
					long nextLogicalPos =
						Chunk.ChunkHeader.GetLocalLogPosition(recordLogPos + length + 2 * sizeof(int));

					return new RawReadResult(true, nextLogicalPos, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			public RecordReadResult TryReadLast(ITransactionFileTracker tracker) {
				return TryReadClosestBackward(Chunk.LogicalDataSize, tracker);
			}

			public RecordReadResult TryReadClosestBackward(long logicalPosition, ITransactionFileTracker tracker) {
				if (Chunk.ChunkFooter.MapCount == 0)
					return RecordReadResult.Failure;

				var workItem = Chunk.GetReaderWorkItem(tracker);
				try {
					var actualPosition = TranslateClosestForwardPosition(workItem, logicalPosition);
					// here we allow actualPosition == _physicalDataSize as we can read backward the very last record that way
					if (actualPosition == -1 || actualPosition > Chunk.PhysicalDataSize)
						return RecordReadResult.Failure;

					int length;
					ILogRecord record;
					if (!TryReadBackwardInternal(workItem, actualPosition, out length, out record))
						return RecordReadResult.Failure;

					long nextLogicalPos = Chunk.ChunkHeader.GetLocalLogPosition(record.LogPosition);
					return new RecordReadResult(true, nextLogicalPos, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			private int TranslateClosestForwardPosition(ReaderWorkItem workItem, long logicalPosition) {
				var midpoints = GetOrCreateMidPoints(workItem);
				if (midpoints == null)
					return TranslateClosestForwardWithoutMidpoints(workItem, logicalPosition, 0,
						Chunk.ChunkFooter.MapCount - 1);
				return TranslateClosestForwardWithMidpoints(workItem, midpoints, logicalPosition);
			}

			private int TranslateClosestForwardWithMidpoints(ReaderWorkItem workItem, Midpoint[] midpoints, long pos) {
				// to allow backward reading of the last record, forward read will decline anyway
				if (pos > midpoints[midpoints.Length - 1].LogPos)
					return Chunk.PhysicalDataSize;

				var recordRange = LocatePosRange(midpoints, pos);
				return TranslateClosestForwardWithoutMidpoints(workItem, pos, recordRange.Lower, recordRange.Upper);
			}

			private int TranslateClosestForwardWithoutMidpoints(ReaderWorkItem workItem, long pos, long startIndex,
				long endIndex) {
				PosMap res = ReadPosMap(workItem, endIndex);

				// to allow backward reading of the last record, forward read will decline anyway
				if (pos > res.LogPos)
					return Chunk.PhysicalDataSize;

				long low = startIndex;
				long high = endIndex;
				while (low < high) {
					var mid = low + (high - low) / 2;
					var v = ReadPosMap(workItem, mid);

					if (v.LogPos < pos)
						low = mid + 1;
					else {
						high = mid;
						res = v;
					}
				}

				return res.ActualPos;
			}

			private static Range LocatePosRange(Midpoint[] midpoints, long pos) {
				int lowerMidpoint = LowerMidpointBound(midpoints, pos);
				int upperMidpoint = UpperMidpointBound(midpoints, pos);
				return new Range(midpoints[lowerMidpoint].ItemIndex, midpoints[upperMidpoint].ItemIndex);
			}

			/// <summary>
			/// Returns the index of lower midpoint for given logical position.
			/// Assumes it always exist.
			/// </summary>
			private static int LowerMidpointBound(Midpoint[] midpoints, long pos) {
				int l = 0;
				int r = midpoints.Length - 1;
				while (l < r) {
					int m = l + (r - l + 1) / 2;
					if (midpoints[m].LogPos <= pos)
						l = m;
					else
						r = m - 1;
				}

				return l;
			}

			/// <summary>
			/// Returns the index of upper midpoint for given logical position.
			/// Assumes it always exist.
			/// </summary>
			private static int UpperMidpointBound(Midpoint[] midpoints, long pos) {
				int l = 0;
				int r = midpoints.Length - 1;
				while (l < r) {
					int m = l + (r - l) / 2;
					if (midpoints[m].LogPos >= pos)
						r = m;
					else
						l = m + 1;
				}

				return l;
			}
		}

		private abstract class TFChunkReadSide {
			protected readonly TFChunk Chunk;
			protected readonly ILogger _log = Log.ForContext<TFChunkReader>();

			protected TFChunkReadSide(TFChunk chunk) {
				Ensure.NotNull(chunk, "chunk");
				Chunk = chunk;
			}

			private bool ValidateRecordPosition(long actualPosition) {
				// no space even for length prefix and suffix
				if (actualPosition + 2 * sizeof(int) > Chunk.PhysicalDataSize) {
					_log.Warning(
						"Tried to read actual position {actualPosition}, but there isn't enough space for a record left in the chunk. Chunk's data size: {chunkPhysicalDataSize}",
						actualPosition, Chunk.PhysicalDataSize);
					return false;
				}

				return true;
			}

			private void ValidateRecordLength(int length, long actualPosition) {
				if (length <= 0) {
					throw new InvalidReadException(
						$"Log record at actual pos {actualPosition} has non-positive length: {length}. " +
						$" in chunk {Chunk}.");
				}

				if (length > TFConsts.MaxLogRecordSize) {
					throw new InvalidReadException(
						$"Log record at actual pos {actualPosition} has too large length: {length} bytes, " +
						$"while limit is {TFConsts.MaxLogRecordSize} bytes. In chunk {Chunk}.");
				}

				if (actualPosition + length + 2 * sizeof(int) > Chunk.PhysicalDataSize) {
					throw new UnableToReadPastEndOfStreamException(
						$"There is not enough space to read full record (length prefix: {length}). " +
						$"Actual pre-position: {actualPosition}. Something is seriously wrong in chunk {Chunk}.");
				}
			}

			private void ValidateSuffixLength(int length, int suffixLength, long actualPosition) {
				// verify suffix length == prefix length
				if (suffixLength != length) {
					throw new Exception(
						$"Prefix/suffix length inconsistency: prefix length({length}) != suffix length ({suffixLength}).\n" +
						$"Actual pre-position: {actualPosition}. Something is seriously wrong in chunk {Chunk}.");
				}
			}

			protected bool TryReadForwardInternal(ReaderWorkItem workItem, long actualPosition, out int length,
				out ILogRecord record) {

				length = -1;
				record = null;

				workItem.Stream.Position = GetRawPosition(actualPosition);
				if (!ValidateRecordPosition(actualPosition))
					return false;

				length = workItem.Reader.ReadInt32();
				ValidateRecordLength(length, actualPosition);

				record = LogRecord.ReadFrom(workItem.Reader, length);
				workItem.Tracker.OnRead(record, Chunk.IsCached ?
					ITransactionFileTracker.Source.ChunkCache :
					ITransactionFileTracker.Source.File);

				int suffixLength = workItem.Reader.ReadInt32();
				ValidateSuffixLength(length, suffixLength, actualPosition);

				return true;
			}

			protected bool TryReadForwardRawInternal(ReaderWorkItem workItem, long actualPosition, Func<int, byte[]> getBuffer,
				out int length, out byte[] record) {
				length = -1;
				record = null;

				workItem.Stream.Position = GetRawPosition(actualPosition);
				if (!ValidateRecordPosition(actualPosition))
					return false;

				length = workItem.Reader.ReadInt32();
				ValidateRecordLength(length, actualPosition);

				record = getBuffer(length);

				workItem.Reader.Read(record, 0, length);

				int suffixLength = workItem.Reader.ReadInt32();
				ValidateSuffixLength(length, suffixLength, actualPosition);

				return true;
			}

			protected bool TryReadBackwardInternal(ReaderWorkItem workItem, long actualPosition, out int length,
				out ILogRecord record) {
				length = -1;
				record = null;

				// no space even for length prefix and suffix
				if (actualPosition < 2 * sizeof(int)) {
					_log.Warning(
						"Tried to read actual position {actualPosition}, but the position isn't large enough to contain a record",
						actualPosition);
					return false;
				}

				var realPos = GetRawPosition(actualPosition);
				workItem.Stream.Position = realPos - sizeof(int);

				length = workItem.Reader.ReadInt32();
				if (length <= 0) {
					throw new InvalidReadException(
						string.Format("Log record that ends at actual pos {0} has non-positive length: {1}. "
									  + "In chunk {2}.",
							actualPosition, length, Chunk));
				}

				if (length > TFConsts.MaxLogRecordSize) {
					throw new ArgumentException(
						string.Format("Log record that ends at actual pos {0} has too large length: {1} bytes, "
									  + "while limit is {2} bytes. In chunk {3}.",
							actualPosition, length, TFConsts.MaxLogRecordSize, Chunk));
				}

				if (actualPosition < length + 2 * sizeof(int)) // no space for record + length prefix and suffix
				{
					throw new UnableToReadPastEndOfStreamException(
						string.Format("There is not enough space to read full record (length suffix: {0}). "
									  + "Actual post-position: {1}. Something is seriously wrong in chunk {2}.",
							length, actualPosition, Chunk));
				}

				workItem.Stream.Position = realPos - length - 2 * sizeof(int);

				// verify suffix length == prefix length
				int prefixLength = workItem.Reader.ReadInt32();
				if (prefixLength != length) {
					throw new Exception(
						string.Format("Prefix/suffix length inconsistency: prefix length({0}) != suffix length ({1})"
									  + "Actual post-position: {2}. Something is seriously wrong in chunk {3}.",
							prefixLength, length, actualPosition, Chunk));
				}

				record = LogRecord.ReadFrom(workItem.Reader, length);
				workItem.Tracker.OnRead(record, Chunk.IsCached ?
					ITransactionFileTracker.Source.ChunkCache :
					ITransactionFileTracker.Source.File);

				return true;
			}
		}
	}

	public class InvalidReadException : Exception {
		public InvalidReadException(string message) : base(message) {
		}
	}
}
