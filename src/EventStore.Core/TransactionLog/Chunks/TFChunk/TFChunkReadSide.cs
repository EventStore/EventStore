using System;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk
{
    public partial class TFChunk
    {
        public interface IChunkReadSide
        {
            void Cache();
            void Uncache();

            bool ExistsAt(long logicalPosition);
            RecordReadResult TryReadAt(long logicalPosition);
            RecordReadResult TryReadFirst();
            RecordReadResult TryReadClosestForward(long logicalPosition);
            RecordReadResult TryReadLast();
            RecordReadResult TryReadClosestBackward(long logicalPosition);
        }

        private class TFChunkReadSideUnscavenged: TFChunkReadSide, IChunkReadSide
        {
            public TFChunkReadSideUnscavenged(TFChunk chunk): base(chunk)
            {
                if (chunk.ChunkHeader.IsScavenged)
                    throw new ArgumentException("Scavenged TFChunk passed into unscavenged chunk read side.");
            }

            public void Cache()
            {
                // do nothing
            }

            public void Uncache()
            {
                // do nothing
            }

            public bool ExistsAt(long logicalPosition)
            {
                return logicalPosition >= 0 && logicalPosition < Chunk.LogicalDataSize;
            }

            public RecordReadResult TryReadAt(long logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    if (logicalPosition >= Chunk.LogicalDataSize)
                        return RecordReadResult.Failure;

                    LogRecord record;
                    int length;
                    var result = TryReadForwardInternal(workItem, logicalPosition, out length, out record);
                    return new RecordReadResult(result, -1, record, length, 0);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            public RecordReadResult TryReadFirst()
            {
                return TryReadClosestForward(0);
            }

            public RecordReadResult TryReadClosestForward(long logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    if (logicalPosition >= Chunk.LogicalDataSize)
                        return RecordReadResult.Failure;

                    int length;
                    LogRecord record;
                    if (!TryReadForwardInternal(workItem, logicalPosition, out length, out record))
                        return RecordReadResult.Failure;

                    long nextLogicalPos = record.GetNextLogPosition(logicalPosition, length, 0);
                    return new RecordReadResult(true, nextLogicalPos, record, length, 0);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            public RecordReadResult TryReadLast()
            {
                return TryReadClosestBackward(Chunk.LogicalDataSize);
            }

            public RecordReadResult TryReadClosestBackward(long logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    // here we allow actualPosition == _logicalDataSize as we can read backward the very last record that way
                    if (logicalPosition > Chunk.LogicalDataSize)
                        return RecordReadResult.Failure;

                    int length;
                    LogRecord record;
                    if (!TryReadBackwardInternal(workItem, logicalPosition, out length, out record))
                        return RecordReadResult.Failure;

                    long nextLogicalPos = record.GetPrevLogPosition(logicalPosition, length, 0);
                    return new RecordReadResult(true, nextLogicalPos, record, length, 0);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }
        }

        private class TFChunkReadSideScavenged : TFChunkReadSide, IChunkReadSide
        {
            private Midpoint[] _midpoints;

            public TFChunkReadSideScavenged(TFChunk chunk)
                : base(chunk)
            {
                if (!chunk.ChunkHeader.IsScavenged)
                    throw new ArgumentException(string.Format("Chunk provided is not scavenged: {0}", chunk));
            }

            public void Uncache()
            {
                _midpoints = null;
            }

            public void Cache()
            {
                _midpoints = PopulateMidpoints(Chunk.MidpointsDepth);
            }

            private Midpoint[] PopulateMidpoints(int depth)
            {
                if (depth > 31)
                    throw new ArgumentOutOfRangeException("depth", "Depth too for midpoints.");

                if (Chunk.ChunkFooter.MapCount == 0) // empty chunk
                    return null;

                ReaderWorkItem workItem = null;
                try
                {
                    workItem = Chunk.GetReaderWorkItem();

                    int midPointsCnt = 1 << depth;
                    int segmentSize;
                    Midpoint[] midpoints;
                    var mapCount = Chunk.ChunkFooter.MapCount;
                    if (mapCount < midPointsCnt)
                    {
                        segmentSize = 1; // we cache all items
                        midpoints = new Midpoint[mapCount];
                    }
                    else
                    {
                        segmentSize = mapCount / midPointsCnt;
                        midpoints = new Midpoint[1 + (mapCount + segmentSize - 1) / segmentSize];
                    }

                    for (int x = 0, i = 0, xN = mapCount - 1; x < xN; x += segmentSize, i += 1)
                    {
                        midpoints[i] = new Midpoint(x, ReadPosMap(workItem, x));
                    }

                    // add the very last item as the last midpoint (possibly it is done twice)
                    midpoints[midpoints.Length - 1] = new Midpoint(mapCount - 1, ReadPosMap(workItem, mapCount - 1));
                    return midpoints;
                }
                catch (FileBeingDeletedException)
                {
                    return null;
                }
                catch (OutOfMemoryException)
                {
                    return null;
                }
                finally
                {
                    if (workItem != null)
                        Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            private PosMap ReadPosMap(ReaderWorkItem workItem, long index)
            {
                if (Chunk.ChunkFooter.MapVersion == PosMapVersion.PosMapV3)
                {
                    var pos = ChunkHeader.Size + Chunk.ChunkFooter.PhysicalDataSize + index*PosMap.V3Size;
                    workItem.Stream.Seek(pos, SeekOrigin.Begin);
                    return PosMap.FromV3Format(workItem.Reader);
                }
                if(Chunk.ChunkFooter.MapVersion == PosMapVersion.PosMapV2)
                {
                    var pos = ChunkHeader.Size + Chunk.ChunkFooter.PhysicalDataSize + index*PosMap.V2Size;
                    workItem.Stream.Seek(pos, SeekOrigin.Begin);
                    return PosMap.FromV2Format(workItem.Reader);
                }
                else
                {
                    var pos = ChunkHeader.Size + Chunk.ChunkFooter.PhysicalDataSize + index*PosMap.V1Size;
                    workItem.Stream.Seek(pos, SeekOrigin.Begin);
                    return PosMap.FromV1Format(workItem.Reader);
                }
            }

            public bool ExistsAt(long logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    int lengthOffset;
                    var actualPosition = TranslateExactPosition(workItem, logicalPosition, out lengthOffset);
                    return actualPosition >= 0 && actualPosition < Chunk.PhysicalDataSize;
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            public RecordReadResult TryReadAt(long logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    int lengthOffset;
                    var actualPosition = TranslateExactPosition(workItem, logicalPosition, out lengthOffset);
                    if (actualPosition == -1 || actualPosition >= Chunk.PhysicalDataSize)
                        return RecordReadResult.Failure;

                    LogRecord record;
                    int length;
                    var result = TryReadForwardInternal(workItem, actualPosition, out length, out record);
                    return new RecordReadResult(result, -1, record, length, lengthOffset);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            private int TranslateExactPosition(ReaderWorkItem workItem, long pos, out int lengthOffset)
            {
                var midpoints = _midpoints;
                if (workItem.IsMemory || midpoints == null)
                    return TranslateExactWithoutMidpoints(workItem, pos, 0, Chunk.ChunkFooter.MapCount - 1, out lengthOffset);
                return TranslateExactWithMidpoints(workItem, midpoints, pos, out lengthOffset);
            }

            private int TranslateExactWithoutMidpoints(ReaderWorkItem workItem, long pos, long startIndex, long endIndex, out int lengthOffset)
            {
                long low = startIndex;
                long high = endIndex;
                while (low <= high)
                {
                    var mid = low + (high - low) / 2;
                    var v = ReadPosMap(workItem, mid);

                    if (v.LogPos == pos)
                    {
                        lengthOffset = v.LengthOffset;
                        return v.ActualPos;
                    }
                    if (v.LogPos < pos)
                        low = mid + 1;
                    else
                        high = mid - 1;
                }
                lengthOffset = 0;
                return -1;
            }

            private int TranslateExactWithMidpoints(ReaderWorkItem workItem, Midpoint[] midpoints, long pos, out int lengthOffset)
            {
                if (pos < midpoints[0].LogPos || pos > midpoints[midpoints.Length - 1].LogPos)
                {
                    lengthOffset = 0;
                    return -1;
                }

                var recordRange = LocatePosRange(midpoints, pos);
                return TranslateExactWithoutMidpoints(workItem, pos, recordRange.Lower, recordRange.Upper, out lengthOffset);
            }

            public RecordReadResult TryReadFirst()
            {
                return TryReadClosestForward(0);
            }

            public RecordReadResult TryReadClosestForward(long logicalPosition)
            {
                if (Chunk.ChunkFooter.MapCount == 0)
                    return RecordReadResult.Failure;

                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    int lengthOffset;
                    var actualPosition = TranslateClosestForwardPosition(workItem, logicalPosition, out lengthOffset);
                    if (actualPosition == -1 || actualPosition >= Chunk.PhysicalDataSize)
                        return RecordReadResult.Failure;

                    int length;
                    LogRecord record;
                    if (!TryReadForwardInternal(workItem, actualPosition, out length, out record))
                        return RecordReadResult.Failure;

                    long nextLogicalPos = Chunk.ChunkHeader.GetLocalLogPosition(record.GetNextLogPosition(record.LogPosition, length, lengthOffset));
                    return new RecordReadResult(true, nextLogicalPos, record, length, lengthOffset);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            public RecordReadResult TryReadLast()
            {
                return TryReadClosestBackward(Chunk.LogicalDataSize);
            }

            public RecordReadResult TryReadClosestBackward(long logicalPosition)
            {
                if (Chunk.ChunkFooter.MapCount == 0)
                    return RecordReadResult.Failure;

                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    int lengthOffset;
                    var actualPosition = TranslateClosestForwardPosition(workItem, logicalPosition, out lengthOffset);
                    // here we allow actualPosition == _physicalDataSize as we can read backward the very last record that way
                    if (actualPosition == -1 || actualPosition > Chunk.PhysicalDataSize)
                        return RecordReadResult.Failure;

                    int length;
                    LogRecord record;
                    if (!TryReadBackwardInternal(workItem, actualPosition, out length, out record))
                        return RecordReadResult.Failure;

                    long nextLogicalPos = Chunk.ChunkHeader.GetLocalLogPosition(record.LogPosition);
                    return new RecordReadResult(true, nextLogicalPos, record, length, lengthOffset);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            private int TranslateClosestForwardPosition(ReaderWorkItem workItem, long logicalPosition, out int lengthOffset)
            {
                var midpoints = _midpoints;
                if (workItem.IsMemory || midpoints == null)
                    return TranslateClosestForwardWithoutMidpoints(workItem, logicalPosition, 0, Chunk.ChunkFooter.MapCount - 1, out lengthOffset);
                return TranslateClosestForwardWithMidpoints(workItem, midpoints, logicalPosition, out lengthOffset);
            }

            private int TranslateClosestForwardWithMidpoints(ReaderWorkItem workItem, Midpoint[] midpoints, long pos, out int lengthOffset)
            {
                var recordRange = LocatePosRange(midpoints, pos);
                return TranslateClosestForwardWithoutMidpoints(workItem, pos, recordRange.Lower, recordRange.Upper, out lengthOffset);
            }

            private int TranslateClosestForwardWithoutMidpoints(ReaderWorkItem workItem, long pos, long startIndex, long endIndex, out int lengthOffset)
            {
                PosMap res = ReadPosMap(workItem, endIndex);

                // to allow backward reading of the last record, forward read will decline anyway
                if (pos > res.LogPos)
                {
                    lengthOffset = res.LengthOffset;
                    return Chunk.PhysicalDataSize;
                }

                long low = startIndex;
                long high = endIndex;
                while (low < high)
                {
                    var mid = low + (high - low) / 2;
                    var v = ReadPosMap(workItem, mid);

                    if (v.LogPos < pos)
                        low = mid + 1;
                    else
                    {
                        high = mid;
                        res = v;
                    }
                }
                lengthOffset = res.LengthOffset;
                return res.ActualPos;
            }

            private static Range LocatePosRange(Midpoint[] midpoints, long pos)
            {
                int lowerMidpoint = LowerMidpointBound(midpoints, pos);
                int upperMidpoint = UpperMidpointBound(midpoints, pos);
                return new Range(midpoints[lowerMidpoint].ItemIndex, midpoints[upperMidpoint].ItemIndex);
            }

            /// <summary>
            /// Returns the index of lower midpoint for given logical position.
            /// Assumes it always exist.
            /// </summary>
            private static int LowerMidpointBound(Midpoint[] midpoints, long pos)
            {
                int l = 0;
                int r = midpoints.Length - 1;
                while (l < r)
                {
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
            private static int UpperMidpointBound(Midpoint[] midpoints, long pos)
            {
                int l = 0;
                int r = midpoints.Length - 1;
                while (l < r)
                {
                    int m = l + (r - l) / 2;
                    if (midpoints[m].LogPos >= pos)
                        r = m;
                    else
                        l = m + 1;
                }
                return l;
            }
        }

        private abstract class TFChunkReadSide
        {
            protected readonly TFChunk Chunk;

            protected TFChunkReadSide(TFChunk chunk)
            {
                Ensure.NotNull(chunk, "chunk");
                Chunk = chunk;
            }

            protected bool TryReadForwardInternal(ReaderWorkItem workItem, long actualPosition, out int length, out LogRecord record)
            {
                length = -1;
                record = null;

                workItem.Stream.Position = GetRawPosition(actualPosition);

                if (actualPosition + 2*sizeof(int) > Chunk.PhysicalDataSize) // no space even for length prefix and suffix
                    return false;

                length = workItem.Reader.ReadInt32();
                if (length <= 0)
                {
                    throw new InvalidReadException(
                        string.Format("Log record at actual pos {0} has non-positive length: {1}. "
                                      + " in chunk.", actualPosition, length, Chunk));
                }
                if (length > TFConsts.MaxLogRecordSize)
                {
                    throw new InvalidReadException(
                        string.Format("Log record at actual pos {0} has too large length: {1} bytes, "
                                      + "while limit is {2} bytes. In chunk {3}.",
                                      actualPosition, length, TFConsts.MaxLogRecordSize, Chunk));
                }
                if (actualPosition + length + 2 * sizeof(int) > Chunk.PhysicalDataSize)
                {
                    throw new UnableToReadPastEndOfStreamException(
                        string.Format("There is not enough space to read full record (length prefix: {0}). "
                                      + "Actual pre-position: {1}. Something is seriously wrong in chunk {2}.",
                                      length, actualPosition, Chunk));
                }

                record = LogRecord.ReadFrom(workItem.Reader);

                // verify suffix length == prefix length
                int suffixLength = workItem.Reader.ReadInt32();
                if (suffixLength != length)
                {
                    throw new Exception(
                        string.Format("Prefix/suffix length inconsistency: prefix length({0}) != suffix length ({1}).\n"
                                      + "Actual pre-position: {2}. Something is seriously wrong in chunk {3}.",
                                      length, suffixLength, actualPosition, Chunk));
                }

                return true;
            }

            protected bool TryReadBackwardInternal(ReaderWorkItem workItem, long actualPosition, out int length, out LogRecord record)
            {
                length = -1;
                record = null;

                if (actualPosition < 2 * sizeof(int)) // no space even for length prefix and suffix
                    return false;

                var realPos = GetRawPosition(actualPosition);
                workItem.Stream.Position = realPos - sizeof(int);

                length = workItem.Reader.ReadInt32();
                if (length <= 0)
                {
                    throw new InvalidReadException(
                        string.Format("Log record that ends at actual pos {0} has non-positive length: {1}. "
                                      + "In chunk {2}.",
                                      actualPosition, length, Chunk));
                }
                if (length > TFConsts.MaxLogRecordSize)
                {
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

                workItem.Stream.Position = realPos - length - 2*sizeof(int);

                // verify suffix length == prefix length
                int prefixLength = workItem.Reader.ReadInt32();
                if (prefixLength != length)
                {
                    throw new Exception(
                            string.Format("Prefix/suffix length inconsistency: prefix length({0}) != suffix length ({1})"
                                          + "Actual post-position: {2}. Something is seriously wrong in chunk {3}.",
                                          prefixLength, length, actualPosition, Chunk));
                }
                record = LogRecord.ReadFrom(workItem.Reader);

                return true;
            }
        }
    }

    public class InvalidReadException : Exception {
        public InvalidReadException(string message) : base(message) {}
    }
}