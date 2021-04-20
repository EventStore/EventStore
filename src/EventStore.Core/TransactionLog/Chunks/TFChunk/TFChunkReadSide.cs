using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.DataStructures;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.LogRecords;
using Serilog;
using Range = EventStore.Core.Data.Range;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	public partial class TFChunk {
		public interface IChunkReadSide {
			void Cache();
			void Uncache();

			bool ExistsAt(long logicalPosition);
			RecordReadResult TryReadAt(long logicalPosition);
			RecordReadResult TryReadFirst();
			RecordReadResult TryReadClosestForward(long logicalPosition);
			RecordReadResult TryReadLast();
			RecordReadResult TryReadClosestBackward(long logicalPosition);
		}

		private class TFChunkReadSideUnscavenged : TFChunkReadSide, IChunkReadSide {
			public TFChunkReadSideUnscavenged(TFChunk chunk) : base(chunk) {
				if (chunk.ChunkHeader.IsScavenged)
					throw new ArgumentException("Scavenged TFChunk passed into unscavenged chunk read side.");
			}

			public void Cache() {
				// do nothing
			}

			public void Uncache() {
				// do nothing
			}

			public bool ExistsAt(long logicalPosition) {
				return logicalPosition >= 0 && logicalPosition < Chunk.LogicalDataSize;
			}

			public RecordReadResult TryReadAt(long logicalPosition) {
				var workItem = Chunk.GetReaderWorkItem();
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

			public RecordReadResult TryReadFirst() {
				return TryReadClosestForward(0);
			}

			public RecordReadResult TryReadClosestForward(long logicalPosition) {
				var workItem = Chunk.GetReaderWorkItem();
				try {
					if (logicalPosition >= Chunk.LogicalDataSize)
						return RecordReadResult.Failure;

					int length;
					ILogRecord record;
					if (!TryReadForwardInternal(workItem, logicalPosition, out length, out record))
						return RecordReadResult.Failure;

					long nextLogicalPos = record.GetNextLogPosition(logicalPosition, length);
					return new RecordReadResult(true, nextLogicalPos, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			public RecordReadResult TryReadLast() {
				return TryReadClosestBackward(Chunk.LogicalDataSize);
			}

			public RecordReadResult TryReadClosestBackward(long logicalPosition) {
				var workItem = Chunk.GetReaderWorkItem();
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
			private Midpoint[] _midpoints;
			private bool _optimizeCache;
			private BloomFilter _logPositionsBloomFilter;

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
				_midpoints = null;
			}

			public void Cache() {
				_midpoints = PopulateMidpoints(Chunk.MidpointsDepth);
			}

			public void OptimizeExistsAt() {
				if (_optimizeCache && _logPositionsBloomFilter == null)
					_logPositionsBloomFilter = PopulateBloomFilter();
			}

			public void DeOptimizeExistsAt() {
				if (_logPositionsBloomFilter != null)
					_logPositionsBloomFilter = null;
			}

			private BloomFilter PopulateBloomFilter() {
				var mapCount = Chunk.ChunkFooter.MapCount;
				if (mapCount <= 0)
					return null;

				BloomFilter bf = null;
				double p = 1e-4; //false positive probability

				while (p < 1.0) {
					try {
						bf = new BloomFilter(mapCount, p);
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
					workItem = Chunk.GetReaderWorkItem();

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

			private Midpoint[] PopulateMidpoints(int depth) {
				if (depth > 31)
					throw new ArgumentOutOfRangeException("depth", "Depth too for midpoints.");

				if (Chunk.ChunkFooter.MapCount == 0) // empty chunk
					return null;

				ReaderWorkItem workItem = null;
				try {
					workItem = Chunk.GetReaderWorkItem();

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
				} finally {
					if (workItem != null)
						Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			private PosMap ReadPosMap(ReaderWorkItem workItem, long index) {
				foreach (var posMap in ReadPosMap(workItem, index, 1)) {
					return posMap;
				}

				throw new ArgumentOutOfRangeException("Could not read PosMap at index: " + index);
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

			public bool ExistsAt(long logicalPosition) {
				if (CacheIsOptimized)
					return MayExistAt(logicalPosition);

				var workItem = Chunk.GetReaderWorkItem();
				try {
					var actualPosition = TranslateExactPosition(workItem, logicalPosition);
					return actualPosition >= 0 && actualPosition < Chunk.PhysicalDataSize;
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			public bool MayExistAt(long logicalPosition) {
				/* This function is much faster than ExistsAt. However, it may return false positives (with a very low probability) but never false negatives */
				return _logPositionsBloomFilter.MayExist(logicalPosition);
			}

			public RecordReadResult TryReadAt(long logicalPosition) {
				var workItem = Chunk.GetReaderWorkItem();
				try {
					var actualPosition = TranslateExactPosition(workItem, logicalPosition);
					if (actualPosition == -1 || actualPosition >= Chunk.PhysicalDataSize) {
						_log.Warning(
							"Tried to read actual position {actualPosition}, translated from logPosition {logicalPosition}, " +
							"which is greater than the chunk's physical size of {chunkPhysicalSize}",
							actualPosition, logicalPosition, Chunk.PhysicalDataSize);
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
				var midpoints = _midpoints;
				if (workItem.IsMemory || midpoints == null)
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

			public RecordReadResult TryReadFirst() {
				return TryReadClosestForward(0);
			}

			public RecordReadResult TryReadClosestForward(long logicalPosition) {
				if (Chunk.ChunkFooter.MapCount == 0)
					return RecordReadResult.Failure;

				var workItem = Chunk.GetReaderWorkItem();
				try {
					var actualPosition = TranslateClosestForwardPosition(workItem, logicalPosition);
					if (actualPosition == -1 || actualPosition >= Chunk.PhysicalDataSize)
						return RecordReadResult.Failure;

					int length;
					ILogRecord record;
					if (!TryReadForwardInternal(workItem, actualPosition, out length, out record))
						return RecordReadResult.Failure;

					long nextLogicalPos =
						Chunk.ChunkHeader.GetLocalLogPosition(record.GetNextLogPosition(record.LogPosition, length));
					return new RecordReadResult(true, nextLogicalPos, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			public RecordReadResult TryReadLast() {
				return TryReadClosestBackward(Chunk.LogicalDataSize);
			}

			public RecordReadResult TryReadClosestBackward(long logicalPosition) {
				if (Chunk.ChunkFooter.MapCount == 0)
					return RecordReadResult.Failure;

				var workItem = Chunk.GetReaderWorkItem();
				try {
					var actualPosition = TranslateClosestForwardPosition(workItem, logicalPosition);
					// here we allow actualPosition == _physicalDataSize as we can read backward the very last record that way
					if (actualPosition == -1 || actualPosition > Chunk.PhysicalDataSize)
						return RecordReadResult.Failure;

					int length;
					ILogRecord record;
					if (!TryReadBackwardInternal(workItem, actualPosition, out length, out record))
						return RecordReadResult.Failure;

					long nextLogicalPos = Chunk.ChunkHeader.GetLocalLogPosition(record.LogPosition); //qq ?????
					return new RecordReadResult(true, nextLogicalPos, record, length);
				} finally {
					Chunk.ReturnReaderWorkItem(workItem);
				}
			}

			private int TranslateClosestForwardPosition(ReaderWorkItem workItem, long logicalPosition) {
				var midpoints = _midpoints;
				if (workItem.IsMemory || midpoints == null)
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

			//qq tentative plan: if the actual position is the beginning of a log record, we read that whole
			// record and return it as normal. length is the length of the record excluding the framing (as normal)
			//
			// note the length is what gets passed to the GetNextLogPosition method to determine where to look up next
			//
			// if position is a subrecord (e.g. an event in a write record) (which we will know because the
			// int at that position is negative) then we will return that subrecord, implementing ILogRecord.
			// length in this case is a little bit fiddly.. is it the offset to the next location that we want to read
			// or is it the more natural length of the current record. the two a different because there is further to jump
			// to the next record if this is the last subrecord, than if there is another subrecord after this one.
			// remember also that we want to be able to read backwards.

			protected bool TryReadForwardInternal(ReaderWorkItem workItem, long actualPosition, out int length,
				out ILogRecord record) {
				length = -1;
				record = null;

				workItem.Stream.Position = GetRawPosition(actualPosition);

				// no space even for length prefix and suffix
				if (actualPosition + 2 * sizeof(int) > Chunk.PhysicalDataSize) {
					_log.Warning(
						"Tried to read actual position {actualPosition}, but there isn't enough space for a record left in the chunk. Chunk's data size: {chunkPhysicalDataSize}",
						actualPosition, Chunk.PhysicalDataSize);
					return false;
				}

				//qq maybe we should threadstatic cache the log record here so that if the actual position (or raw position or whatever) lies within
				// the record we just read, then we can avoid having to reposition the reader at all.
				// i think we want this anyway because the index does a series of stabbing reads. and if we have made that efficient then maybe its fine for the all reader to work the same way.
				// (btw do we need to cache the last two (instead of one)? if we are skipping resolving links? or perhaps not, because linke resolution comes later.

				length = workItem.Reader.ReadInt32();
				int subRecordOffset = 0;
				if (length <= 0) {
					var followNegativeOffset = true; //qq
					if (length < 0 && followNegativeOffset) {
						// we found a negative number, treat it as an offset to the beginning of the record data
						subRecordOffset = -length;

						// first int to skip over the other offset
						// second int to skip over the length framing of the main record
						var totalOffset = sizeof(int) + subRecordOffset + sizeof(int);
						//qq requires the beginning of the record be in the same chunk, but thats pretty much required in tf chunk reading a single record anyway
						// consider whether we want to set actualpositoin here so the subsequent checks and messages are for the whole record
						actualPosition -= totalOffset;
						// and moving the stream position an additional 4 because of the size (int) we just read
						workItem.Stream.Position -= totalOffset + sizeof(int);
						length = workItem.Reader.ReadInt32();

						//qq maybe change this to be a recursive call actually. ^

						//qqqqqqqqqqqq should length be set to the length of the whole record, or the length from the original actualposition to the end of the record?
						//qqqqq maybe subtracting the 8 bytes also.
					}

					//qq the error message should be adjusted if we just followed an offset
					if (length <= 0) {
						throw new InvalidReadException(
							string.Format("Log record at actual pos {0} has non-positive length: {1}. "
										  + " in chunk {2}.", actualPosition, length, Chunk));
					}
				}

				if (length > TFConsts.MaxLogRecordSize) {
					throw new InvalidReadException(
						string.Format("Log record at actual pos {0} has too large length: {1} bytes, "
									  + "while limit is {2} bytes. In chunk {3}.",
							actualPosition, length, TFConsts.MaxLogRecordSize, Chunk));
				}

				if (actualPosition + length + 2 * sizeof(int) > Chunk.PhysicalDataSize) {
					throw new UnableToReadPastEndOfStreamException(
						string.Format("There is not enough space to read full record (length prefix: {0}). "
									  + "Actual pre-position: {1}. Something is seriously wrong in chunk {2}.",
							length, actualPosition, Chunk));
				}

				//qq mybe instead of passing in subrecordoffset here we should make it a recursive call above and then get the subrecord through the ilogrecord interface
				record = LogRecord.ReadFrom(workItem.Reader, length, subRecordOffset, out var lengthOut);
				//qqqq gotta adjust the length here so that the next read will hit the next event, or the next record if this is the last event. 
				// finding the next record is easy, in fact thats what length has already measured.
				// what about finding the next event.... maybe an out parameter in the ReadFrom method, or add something to the ILogRecord interface
				// but leaning the former to avoid having to carry the information around in the log record.

				// verify suffix length == prefix length
				int suffixLength = workItem.Reader.ReadInt32();
				if (suffixLength != length) {
					throw new Exception(
						string.Format("Prefix/suffix length inconsistency: prefix length({0}) != suffix length ({1}).\n"
									  + "Actual pre-position: {2}. Something is seriously wrong in chunk {3}.",
							length, suffixLength, actualPosition, Chunk));
				}

				length = lengthOut;

				return true;
			}

			// actualPosition is the PostPosition of the record we want to read.
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
					var followNegativeOffset = true; //qq
					if (length <= 0 && followNegativeOffset) {
						// we found a non-positive reading backwards. it is the negative index of the subrecord
						// at this pre-position.
						var prevOffset = -length;
						//qq add integrity checks probably.
						var subRecordOffset = -workItem.Reader.ReadInt32();

						if (prevOffset == 0) {
							// this is the first subrecord in the record, read the previous record backward
							var totalOffset = sizeof(int) + subRecordOffset + sizeof(int);
							var recordActualPosition = actualPosition - totalOffset;
							return TryReadBackwardInternal(workItem, recordActualPosition, out length, out record);
						} else {
							// this is not the first subrecord in the record. read this record and
							// get the subrecord to the left of here.
							var subRecordActualPosition = actualPosition - subRecordOffset + prevOffset;

							// the record that we want to read is to the left of where we are now
							//   we can read the main record that we are in and then get the previous subrecord
							//   BUT if we are the first subrecord then we need to jump to a previous record
							// and how do we even know if we are the first?
							// we need to set length and position such that
							// we can reposition leaving us at the start of the write record.
							//qq looks like there is not sensible way to proceed from here
							return TryReadForwardInternal(workItem, subRecordActualPosition, out length, out record);
						}
					}

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

				//qqq tbd whether we want events or stream writes when reading backwards, for now leaving it as stream writes
				//qq no we need to specific eent lookups here because the user might do that with their $all subscription
				record = LogRecord.ReadFrom(workItem.Reader, length, subrecordOffset: 0, out _);

				return true;
			}
		}
	}

	public class InvalidReadException : Exception {
		public InvalidReadException(string message) : base(message) {
		}
	}
}
