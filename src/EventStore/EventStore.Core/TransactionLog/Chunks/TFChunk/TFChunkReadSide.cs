// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Diagnostics;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk
{
    public partial class TFChunk
    {
        public interface IChunkReadSide
        {
            RecordReadResult TryReadAt(int logicalPosition);
            RecordReadResult TryReadFirst();
            RecordReadResult TryReadClosestForward(int logicalPosition);
            RecordReadResult TryReadLast();
            RecordReadResult TryReadClosestBackward(int logicalPosition);
        }

        private class TFChunkReadSideUnscavenged: TFChunkReadSide, IChunkReadSide
        {
            public TFChunkReadSideUnscavenged(TFChunk chunk): base(chunk)
            {
                if (chunk.IsReadOnly && chunk.ChunkFooter.MapCount > 0)
                    throw new ArgumentException("Scavenged TFChunk passed into unscavenged chunk read side.");
            }

            public RecordReadResult TryReadAt(int logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    if (logicalPosition >= Chunk.ActualDataSize)
                        return RecordReadResult.Failure;

                    LogRecord record;
                    int length;
                    var result = TryReadForwardInternal(workItem, logicalPosition, out length, out record);
                    return new RecordReadResult(result, -1, record, length);
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

            public RecordReadResult TryReadClosestForward(int logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    if (logicalPosition >= Chunk.ActualDataSize)
                        return RecordReadResult.Failure;

                    int length;
                    LogRecord record;
                    if (!TryReadForwardInternal(workItem, logicalPosition, out length, out record))
                        return RecordReadResult.Failure;

                    int nextLogicalPos = logicalPosition + length + 2*sizeof(int);
                    return new RecordReadResult(true, nextLogicalPos, record, length);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            public RecordReadResult TryReadLast()
            {
                return TryReadClosestBackward(Chunk.ActualDataSize);
            }

            public RecordReadResult TryReadClosestBackward(int logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    // here we allow actualPosition == _actualDataSize as we can read backward the very last record that way
                    if (logicalPosition > Chunk.ActualDataSize)
                        return RecordReadResult.Failure;

                    int length;
                    LogRecord record;
                    if (!TryReadBackwardInternal(workItem, logicalPosition, out length, out record))
                        return RecordReadResult.Failure;

                    int nextLogicalPos = logicalPosition - length - 2 * sizeof(int);
                    return new RecordReadResult(true, nextLogicalPos, record, length);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }
        }

        private class TFChunkReadSideScavenged : TFChunkReadSide, IChunkReadSide
        {
            public TFChunkReadSideScavenged(TFChunk chunk): base(chunk)
            {
                Ensure.Positive(chunk.ChunkFooter.MapCount, "chunk.ChunkFooter.MapCount");
            }

            public RecordReadResult TryReadAt(int logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    var actualPosition = TranslateExactPosition(workItem, logicalPosition);
                    if (actualPosition == -1 || actualPosition >= Chunk.ActualDataSize)
                        return RecordReadResult.Failure;

                    LogRecord record;
                    int length;
                    var result = TryReadForwardInternal(workItem, actualPosition, out length, out record);
                    return new RecordReadResult(result, -1, record, length);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            private int TranslateExactPosition(ReaderWorkItem workItem, int pos)
            {
                var midpoints = Chunk._midpoints;
                if (workItem.IsMemory || midpoints == null)
                    return TranslateExactWithoutMidpoints(workItem, pos, 0, Chunk.ChunkFooter.MapCount - 1);
                return TranslateExactWithMidpoints(workItem, midpoints, pos);
            }

            private int TranslateExactWithoutMidpoints(ReaderWorkItem workItem, int pos, int startIndex, int endIndex)
            {
                int low = startIndex;
                int high = endIndex;
                while (low <= high)
                {
                    var mid = low + (high - low) / 2;
                    var v = Chunk.ReadPosMap(workItem, mid);

                    if (v.LogPos == pos)
                        return v.ActualPos;
                    if (v.LogPos < pos)
                        low = mid + 1;
                    else
                        high = mid - 1;
                }
                return -1;
            }

            private int TranslateExactWithMidpoints(ReaderWorkItem workItem, Midpoint[] midpoints, int pos)
            {
                if (pos < midpoints[0].LogPos || pos > midpoints[midpoints.Length - 1].LogPos)
                    return -1;

                var recordRange = LocatePosRange(midpoints, pos);
                return TranslateExactWithoutMidpoints(workItem, pos, recordRange.Item1, recordRange.Item2);
            }

            public RecordReadResult TryReadFirst()
            {
                if (Chunk.ActualDataSize == 0)
                    return RecordReadResult.Failure;

                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    LogRecord record;
                    int length;
                    if (!TryReadForwardInternal(workItem, 0, out length, out record))
                        return RecordReadResult.Failure;

                    int nextLogicalPos = Chunk.ChunkFooter.MapCount >= 2
                                         ? Chunk.ReadPosMap(workItem, 1).LogPos
                                         : (int)(record.Position % Chunk.ChunkHeader.ChunkSize) + length + 2*sizeof(int);
                    return new RecordReadResult(true, nextLogicalPos, record, length);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            public RecordReadResult TryReadClosestForward(int logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    var pos = TranslateClosestForwardPosition(workItem, logicalPosition);
                    var actualPosition = pos.Item1;
                    if (actualPosition == -1 || actualPosition >= Chunk.ActualDataSize)
                        return RecordReadResult.Failure;

                    int length;
                    LogRecord record;
                    if (!TryReadForwardInternal(workItem, actualPosition, out length, out record))
                        return RecordReadResult.Failure;

                    int nextLogicalPos = pos.Item2 + 1 < Chunk.ChunkFooter.MapCount
                                         ? Chunk.ReadPosMap(workItem, pos.Item2 + 1).LogPos
                                         : (int)(record.Position % Chunk.ChunkHeader.ChunkSize) + length + 2*sizeof(int);
                    return new RecordReadResult(true, nextLogicalPos, record, length);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            public RecordReadResult TryReadLast()
            {
                if (Chunk.ActualDataSize == 0)
                    return RecordReadResult.Failure;

                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    LogRecord record;
                    int length;
                    if (!TryReadBackwardInternal(workItem, Chunk.ActualDataSize, out length, out record))
                        return RecordReadResult.Failure;

                    int nextLogicalPos;
                    if (Chunk.IsReadOnly && Chunk.ChunkFooter.MapSize > 0)
                    {
                        nextLogicalPos = Chunk.ChunkFooter.MapCount > 1
                                                 ? Chunk.ReadPosMap(workItem, Chunk.ChunkFooter.MapCount - 1).LogPos
                                                 : (int)(record.Position % Chunk.ChunkHeader.ChunkSize);
                    }
                    else
                        nextLogicalPos = Chunk.ActualDataSize - length - 2 * sizeof(int);
                    return new RecordReadResult(true, nextLogicalPos, record, length);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            public RecordReadResult TryReadClosestBackward(int logicalPosition)
            {
                var workItem = Chunk.GetReaderWorkItem();
                try
                {
                    var pos = TranslateClosestForwardPosition(workItem, logicalPosition);
                    var actualPosition = pos.Item1;
                    // here we allow actualPosition == _actualDataSize as we can read backward the very last record that way
                    if (actualPosition == -1 || actualPosition > Chunk.ActualDataSize)
                        return RecordReadResult.Failure;

                    int length;
                    LogRecord record;
                    if (!TryReadBackwardInternal(workItem, actualPosition, out length, out record))
                        return RecordReadResult.Failure;

                    int nextLogicalPos;
                    if (Chunk.IsReadOnly && Chunk.ChunkFooter.MapSize > 0)
                    {
                        nextLogicalPos = pos.Item2 > 0
                                                 ? Chunk.ReadPosMap(workItem, pos.Item2 - 1).LogPos
                                                 : (int)(record.Position % Chunk.ChunkHeader.ChunkSize);
                    }
                    else
                        nextLogicalPos = actualPosition - length - 2 * sizeof(int);

                    return new RecordReadResult(true, nextLogicalPos, record, length);
                }
                finally
                {
                    Chunk.ReturnReaderWorkItem(workItem);
                }
            }

            private Tuple<int, int> TranslateClosestForwardPosition(ReaderWorkItem workItem, int logicalPosition)
            {
                if (!Chunk.IsReadOnly || Chunk.ChunkFooter.MapSize == 0)
                {
                    // this is mostly for ability to read closest backward from the very end
                    var logicalPos = Math.Min(Chunk.ActualDataSize, logicalPosition);
                    return Tuple.Create(logicalPos, -1);
                }

                var midpoints = Chunk._midpoints;
                if (workItem.IsMemory || midpoints == null)
                {
                    return TranslateClosestForwardWithoutMidpoints(workItem, logicalPosition, 0, Chunk.ChunkFooter.MapCount - 1);
                }
                return TranslateClosestForwardWithMidpoints(workItem, midpoints, logicalPosition);
            }

            private Tuple<int, int> TranslateClosestForwardWithMidpoints(ReaderWorkItem workItem, Midpoint[] midpoints, int pos)
            {
                if (pos > midpoints[midpoints.Length - 1].LogPos)
                    return Tuple.Create(Chunk.ActualDataSize, midpoints.Length); // to allow backward reading of the last record, forward read will decline anyway

                var recordRange = LocatePosRange(midpoints, pos);
                return TranslateClosestForwardWithoutMidpoints(workItem, pos, recordRange.Item1, recordRange.Item2);
            }

            private Tuple<int, int> TranslateClosestForwardWithoutMidpoints(ReaderWorkItem workItem, int pos, int startIndex, int endIndex)
            {
                PosMap res = Chunk.ReadPosMap(workItem, endIndex);

                if (pos > res.LogPos)
                    return Tuple.Create(Chunk.ActualDataSize, endIndex + 1); // to allow backward reading of the last record, forward read will decline anyway
                int low = startIndex;
                int high = endIndex;
                while (low < high)
                {
                    var mid = low + (high - low) / 2;
                    var v = Chunk.ReadPosMap(workItem, mid);

                    if (v.LogPos < pos)
                        low = mid + 1;
                    else
                    {
                        high = mid;
                        res = v;
                    }
                }
                return Tuple.Create(res.ActualPos, high);
            }

            private static Tuple<int, int> LocatePosRange(Midpoint[] midpoints, int pos)
            {
                int lowerMidpoint = LowerMidpointBound(midpoints, pos);
                int upperMidpoint = UpperMidpointBound(midpoints, pos);
                return Tuple.Create(midpoints[lowerMidpoint].ItemIndex, midpoints[upperMidpoint].ItemIndex);
            }

            /// <summary>
            /// Returns the index of lower midpoint for given logical position.
            /// Assumes it always exist.
            /// </summary>
            private static int LowerMidpointBound(Midpoint[] midpoints, int pos)
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
            private static int UpperMidpointBound(Midpoint[] midpoints, int pos)
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

            protected bool TryReadForwardInternal(ReaderWorkItem workItem, int actualPosition, out int length, out LogRecord record)
            {
                length = -1;
                record = null;

                workItem.Stream.Position = GetRealPosition(actualPosition);

                if (actualPosition + 2*sizeof(int) > Chunk.ActualDataSize) // no space even for length prefix and suffix
                    return false;

                length = workItem.Reader.ReadInt32();
                if (length <= 0)
                {
                    throw new ArgumentException(
                            string.Format("Log record at actual pos {0} has non-positive length: {1}. "
                                          + "Something is seriously wrong in chunk {2}-{3} ({4}).",
                                          actualPosition,
                                          length,
                                          Chunk.ChunkHeader.ChunkStartNumber,
                                          Chunk.ChunkHeader.ChunkEndNumber,
                                          Chunk.FileName));
                }
                if (length > TFConsts.MaxLogRecordSize)
                {
                    throw new ArgumentException(
                            string.Format("Log record at actual pos {0} has too large length: {1} bytes, "
                                          + "while limit is {2} bytes. Something is seriously wrong in chunk {3}-{4} ({5}).",
                                          actualPosition,
                                          length,
                                          TFConsts.MaxLogRecordSize,
                                          Chunk.ChunkHeader.ChunkStartNumber,
                                          Chunk.ChunkHeader.ChunkEndNumber,
                                          Chunk.FileName));
                }

                if (actualPosition + length + 2*sizeof(int) > Chunk.ActualDataSize)
                    throw new UnableToReadPastEndOfStreamException(
                        string.Format("There is not enough space to read full record (record length according to length prefix: {0}).", length));

                record = LogRecord.ReadFrom(workItem.Reader);

                int suffixLength;
                Debug.Assert((suffixLength = workItem.Reader.ReadInt32()) == length); // verify suffix length == prefix length

                return true;
            }

            protected static bool TryReadBackwardInternal(ReaderWorkItem workItem, int actualPosition, out int length, out LogRecord record)
            {
                length = -1;
                record = null;

                if (actualPosition < 2 * sizeof(int)) // no space even for length prefix and suffix 
                    return false;

                var realPos = GetRealPosition(actualPosition);
                workItem.Stream.Position = realPos - sizeof(int);

                length = workItem.Reader.ReadInt32();
                if (length <= 0)
                {
                    throw new ArgumentException(
                            string.Format("Log record that ends at actual pos {0} has non-positive length: {1}. "
                                          + "Something is seriously wrong.",
                                          actualPosition,
                                          length));
                }
                if (length > TFConsts.MaxLogRecordSize)
                {
                    throw new ArgumentException(
                            string.Format("Log record that ends at actual pos {0} has too large length: {1} bytes, "
                                          + "while limit is {2} bytes.",
                                          actualPosition,
                                          length,
                                          TFConsts.MaxLogRecordSize));
                }

                if (actualPosition < length + 2 * sizeof(int)) // no space for record + length prefix and suffix 
                    throw new UnableToReadPastEndOfStreamException(
                        string.Format("There is not enough space to read full record (record length according to length suffix: {0}).", length));

                workItem.Stream.Position = realPos - length - sizeof(int);
                record = LogRecord.ReadFrom(workItem.Reader);

#if DEBUG
                workItem.Stream.Position = realPos - length - 2 * sizeof(int);
                var prefixLength = workItem.Reader.ReadInt32();
                Debug.Assert(prefixLength == length);
#endif
                return true;
            }
        }
    }
}