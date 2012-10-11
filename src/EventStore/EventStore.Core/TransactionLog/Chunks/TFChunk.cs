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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Util;

namespace EventStore.Core.TransactionLog.Chunks
{
#if __MonoCS__
    using ConcurrentReaderWorkItemQueue = Common.ConcurrentCollections.ConcurrentQueue<ReaderWorkItem>;
#else
    using ConcurrentReaderWorkItemQueue = System.Collections.Concurrent.ConcurrentQueue<ReaderWorkItem>;
#endif

    public unsafe class TFChunk : IDisposable
    {
        public const int Version = 1;

        public const int WriteBufferSize = 4096;
        public const int ReadBufferSize = 512;

        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunk>();

        public bool IsReadOnly { get { return _isReadonly; } }
        public bool IsCached { get { return _cached; } }
        public int ActualDataSize { get { return _actualDataSize; } }
        public string FileName { get { return _filename; } }

        public ChunkHeader ChunkHeader { get { return _chunkHeader; } }
        public ChunkFooter ChunkFooter { get { return _chunkFooter; } }
        
        private readonly string _filename;
        private volatile bool _isReadonly;
        private ChunkHeader _chunkHeader;
        private ChunkFooter _chunkFooter;
        
        private readonly int _maxReadThreads;
        private readonly ConcurrentReaderWorkItemQueue _streams = new ConcurrentReaderWorkItemQueue();
        private ConcurrentReaderWorkItemQueue _memoryStreams;
        private WriterWorkItem _writerWorkItem;
        private volatile int _actualDataSize;

        private byte* _cachedData;
        private volatile bool _cached;
        private int _cachedDataLength;

        private Midpoint[] _midpoints;
        private readonly int _midpointsDepth;

        private readonly ManualResetEvent _destroyEvent = new ManualResetEvent(false);
        private volatile bool _selfdestructin54321;
        private volatile bool _deleteFile;

        private TFChunk(string filename, int maxReadThreads, int midpointsDepth)
        {
            Ensure.NotNullOrEmpty(filename, "filename");
            Ensure.Positive(maxReadThreads, "maxReadThreads");

            _filename = filename;
            _maxReadThreads = maxReadThreads;
            _midpointsDepth = midpointsDepth;
        }

        ~TFChunk()
        {
            FreeCachedData();
        }

        private void InitCompleted()
        {
            if (!File.Exists(_filename))
                throw new CorruptDatabaseException(new ChunkNotFoundException(_filename));

            _isReadonly = true;
            CreateReaderStreams();
            var reader = GetReaderWorkItem();
            try
            {
                Debug.Assert(!reader.IsMemory);

                _chunkFooter = ReadFooter(reader.Stream);
                _actualDataSize = _chunkFooter.ActualDataSize;
                _chunkHeader = ReadHeader(reader.Stream);

                var expectedFileSize = _chunkFooter.ActualChunkSize + _chunkFooter.MapSize + ChunkHeader.Size + ChunkFooter.Size;
                if (reader.Stream.Length != expectedFileSize)
                {
                    throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                        string.Format("Chunk file '{0}' should have file size {1} bytes, but instead has {2} bytes length.",
                                      _filename,
                                      expectedFileSize,
                                      reader.Stream.Length)));
                }

                _midpoints = PopulateMidpoints(_midpointsDepth);
            }
            finally
            {
                ReturnReaderWorkItem(reader);
            }

            SetAttributes();
            //VerifyFileHash();
        }

        private void InitNew(ChunkHeader chunkHeader)
        {
            Ensure.NotNull(chunkHeader, "chunkHeader");

            _isReadonly = false;
            _chunkHeader = chunkHeader;
            _actualDataSize = 0;
            CreateWriterWorkItemForNewChunk(chunkHeader);
            CreateReaderStreams();

            SetAttributes();
            //VerifyFileHash();
        }

        private void InitOngoing(int writePosition)
        {
            Ensure.Nonnegative(writePosition, "writePosition");
            if (!File.Exists(_filename))
                throw new CorruptDatabaseException(new ChunkNotFoundException(_filename));

            _isReadonly = false;
            _actualDataSize = writePosition;
            CreateWriterWorkItemForExistingChunk(writePosition, out _chunkHeader);
            CreateReaderStreams();

            var expectedFileSize = _chunkHeader.ChunkSize + ChunkHeader.Size + ChunkFooter.Size;
            if (_writerWorkItem.Stream.Length != expectedFileSize)
            {
                throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                    string.Format("Chunk file '{0}' should have file size {1} bytes, but instead has {2} bytes length.",
                                  _filename,
                                  expectedFileSize,
                                  _writerWorkItem.Stream.Length)));
            }

            SetAttributes();
            //VerifyFileHash();
        }

        public static TFChunk FromCompletedFile(string filename)
        {
            var chunk = new TFChunk(filename, TFConsts.TFChunkReaderCount, TFConsts.MidpointsDepth);
            try
            {
                chunk.InitCompleted();
            }
            catch
            {
                chunk.Dispose();
                throw;
            }
            return chunk;
        }

        public static TFChunk FromOngoingFile(string filename, int writePosition)
        {
            var chunk = new TFChunk(filename, TFConsts.TFChunkReaderCount, TFConsts.MidpointsDepth);
            try
            {
                chunk.InitOngoing(writePosition);
            }
            catch
            {
                chunk.Dispose();
                throw;
            }
            return chunk;
        }

        public static TFChunk CreateNew(string filename, int chunkSize, int chunkNumber, int chunkScavengeVersion)
        {
            var chunkHeader = new ChunkHeader(Version, chunkSize, chunkNumber, chunkNumber, chunkScavengeVersion);
            var chunk = new TFChunk(filename, TFConsts.TFChunkReaderCount, TFConsts.MidpointsDepth);
            try
            {
                chunk.InitNew(chunkHeader);
            }
            catch
            {
                chunk.Dispose();
                throw;
            }
            return chunk;
        }

        private void CreateReaderStreams()
        {
            for (int i = 0; i < _maxReadThreads; i++)
            {
                var stream = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                                            ReadBufferSize, FileOptions.RandomAccess);
                var reader = new BinaryReader(stream);
                _streams.Enqueue(new ReaderWorkItem(stream, reader, false));
            }
        }

        private void CreateWriterWorkItemForNewChunk(ChunkHeader chunkHeader)
        {
            var md5 = MD5.Create();
            var stream = new FileStream(_filename, FileMode.Create, FileAccess.ReadWrite, FileShare.Read,
                                        WriteBufferSize, FileOptions.SequentialScan);
            var writer = new BinaryWriter(stream);
            stream.SetLength(chunkHeader.ChunkSize + ChunkHeader.Size + ChunkFooter.Size);
            WriteHeader(md5, stream, chunkHeader);
            _writerWorkItem = new WriterWorkItem(stream, writer, md5);
            Flush();
        }

        private void CreateWriterWorkItemForExistingChunk(int writePosition, out ChunkHeader chunkHeader)
        {
            var md5 = MD5.Create();
            var stream = new FileStream(_filename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read,
                                        WriteBufferSize, FileOptions.SequentialScan);
            var writer = new BinaryWriter(stream);
            try
            {
                chunkHeader = ReadHeader(stream);
            }
            catch
            {
                stream.Dispose();
                ((IDisposable)md5).Dispose();
                throw;
            }
            var realPosition = GetRealPosition(writePosition, inMemory: false);
            stream.Position = realPosition;
            MD5Hash.ContinuousHashFor(md5, stream, 0, realPosition);
            _writerWorkItem = new WriterWorkItem(stream, writer, md5);
        }

        private void WriteHeader(MD5 md5, Stream stream, ChunkHeader chunkHeader)
        {
            var chunkHeaderBytes = chunkHeader.AsByteArray();
            md5.TransformBlock(chunkHeaderBytes, 0, ChunkHeader.Size, null, 0);
            stream.Write(chunkHeaderBytes, 0, ChunkHeader.Size);
        }

        private void SetAttributes()
        {
            Helper.EatException(() =>
            {
                // in mono SetAttributes on non-existing file throws exception, in windows it just works silently.
                File.SetAttributes(_filename, FileAttributes.ReadOnly);
                File.SetAttributes(_filename, FileAttributes.Temporary); // non-intuitive, tells OS to try to cache it!
                File.SetAttributes(_filename, FileAttributes.NotContentIndexed);
            });
        }

        public void VerifyFileHash()
        {
            if (!IsReadOnly)
                throw new InvalidOperationException("You can't verify hash of not-completed TFChunk.");

            var workItem = GetReaderWorkItem();
            try
            {
                var header = ReadHeader(workItem.Stream);
                var footer = ReadFooter(workItem.Stream);

                byte[] hash;
                using (var md5 = MD5.Create())
                {
                    workItem.Stream.Seek(0, SeekOrigin.Begin);
                    // hash header and data
                    MD5Hash.ContinuousHashFor(md5,
                                              workItem.Stream,
                                              0,
                                              ChunkHeader.Size + footer.ActualDataSize);
                    // hash mapping and footer except MD5 hash sum which should always be last
                    MD5Hash.ContinuousHashFor(md5,
                                              workItem.Stream,
                                              ChunkHeader.Size + footer.ActualChunkSize,
                                              footer.MapSize + ChunkFooter.Size - ChunkFooter.ChecksumSize);
                    md5.TransformFinalBlock(new byte[0], 0, 0);
                    hash = md5.Hash;
                }

                if (footer.MD5Hash == null || footer.MD5Hash.Length != hash.Length) 
                    throw new HashValidationException();
                
                for (int i = 0; i < hash.Length; ++i)
                {
                    if (footer.MD5Hash[i] != hash[i])
                        throw new HashValidationException();
                }
            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        private ChunkHeader ReadHeader(Stream stream)
        {
            if (stream.Length < ChunkHeader.Size)
            {
                throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                    string.Format("Chunk file '{0}' is too short to even read ChunkHeader, its size is {1} bytes.",
                                  _filename,
                                  stream.Length)));
            }

            stream.Seek(0, SeekOrigin.Begin);
            var chunkHeader = ChunkHeader.FromStream(stream);
            return chunkHeader;
        }

        private ChunkFooter ReadFooter(Stream stream)
        {
            if (stream.Length < ChunkFooter.Size)
            {
                throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                    string.Format("Chunk file '{0}' is too short to even read ChunkFooter, its size is {1} bytes.",
                                  _filename,
                                  stream.Length)));
            }

            stream.Seek(-ChunkFooter.Size, SeekOrigin.End);
            var footer = ChunkFooter.FromStream(stream);
            return footer;
        }

        private Midpoint[] PopulateMidpoints(int depth)
        {
            if (!_isReadonly || _chunkFooter.MapSize == 0)
                return null;

            var workItem = GetReaderWorkItem();
            try
            {
                int midPointsCnt = 1 << depth;
                int segmentSize;
                Midpoint[] midpoints;
                var mapCount = _chunkFooter.MapSize / sizeof (ulong);
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
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        private PosMap ReadPosMap(ReaderWorkItem workItem, int index)
        {
            var pos = (workItem.IsMemory ? 0 : ChunkHeader.Size) + _chunkFooter.ActualChunkSize + (index << 3);
            workItem.Stream.Seek(pos, SeekOrigin.Begin);
            return new PosMap(workItem.Reader.ReadUInt64());
        }

        public void CacheInMemory()
        {
            if (_cached || _selfdestructin54321)
                return;
            var sw = Stopwatch.StartNew();

            BuildCacheArray();
            _destructedMemStreams = 0;
            _memoryStreams = BuildCacheReaders();
            if (_writerWorkItem != null)
            {
                _writerWorkItem.UnmanagedMemoryStream = 
                    new UnmanagedMemoryStream(_cachedData, _cachedDataLength, _cachedDataLength, FileAccess.ReadWrite);
            }
            _cached = true;

            _midpoints = null;

            Log.Trace("CACHED TFChunk #{0} at {1} in {2}.", _chunkHeader.ChunkStartNumber, Path.GetFileName(_filename), sw.Elapsed);
        }

        private void BuildCacheArray()
        {
            var workItem = GetReaderWorkItem();
            if (workItem.IsMemory)
                throw new InvalidOperationException("When trying to build cache, reader worker is already in-memory reader.");
            try
            {
                _cachedDataLength = _isReadonly ? _chunkFooter.ActualChunkSize + _chunkFooter.MapSize : _chunkHeader.ChunkSize;
                var cachedData = (byte*) Marshal.AllocHGlobal(_cachedDataLength);
                try
                {
                    using (var unmanagedStream = new UnmanagedMemoryStream(cachedData, 
                                                                           _cachedDataLength, 
                                                                           _cachedDataLength, 
                                                                           FileAccess.ReadWrite))
                    {
                        workItem.Stream.Seek(GetRealPosition(0, inMemory: false), SeekOrigin.Begin);
                        var buffer = new byte[4096];
                        int toRead = _cachedDataLength;
                        while (toRead > 0)
                        {
                            int read = workItem.Stream.Read(buffer, 0, Math.Min(toRead, buffer.Length));
                            if (read == 0)
                                break;
                            toRead -= read;
                            unmanagedStream.Write(buffer, 0, read);
                        }
                    }
                }
                catch
                {
                    Marshal.FreeHGlobal((IntPtr) cachedData);
                    throw;
                }
                _cachedData = cachedData;
            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        private ConcurrentReaderWorkItemQueue BuildCacheReaders()
        {
            var queue = new ConcurrentReaderWorkItemQueue();
            for (int i = 0; i < _maxReadThreads; i++)
            {
                var stream = new UnmanagedMemoryStream(_cachedData, _cachedDataLength);
                var reader = new BinaryReader(stream);
                queue.Enqueue(new ReaderWorkItem(stream, reader, true));
            }
            return queue;
        }

        public void UnCacheFromMemory()
        {
            var wasCached = _cached;

            if (!_selfdestructin54321)
                _midpoints = PopulateMidpoints(_midpointsDepth);

            _cached = false;

            var unmanagedMemStream = _writerWorkItem == null ? null : _writerWorkItem.UnmanagedMemoryStream;
            if (unmanagedMemStream != null)
            {
                unmanagedMemStream.Dispose();
                _writerWorkItem.UnmanagedMemoryStream = null;
            }

            TryDestructUnmanagedMemory();

            if (wasCached)
                Log.Trace("UNCACHED TFChunk #{0} at {1}", _chunkHeader.ChunkStartNumber, Path.GetFileName(_filename));
        }

        public RecordReadResult TryReadRecordAt(int logicalPosition)
        {
            var workItem = GetReaderWorkItem();
            try
            {
                LogRecord record;
                var actualPosition = TranslatePosition(workItem, logicalPosition);
                if (actualPosition == -1 || actualPosition >= _actualDataSize)
                    return new RecordReadResult(false, null, -1);
                int length;
                var result = TryReadRecordInternal(workItem, actualPosition, out length, out record);
                return new RecordReadResult(result, record, -1);
            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        public RecordReadResult TryReadFirst()
        {
            var workItem = GetReaderWorkItem();
            try
            {
                LogRecord record;
                if (_actualDataSize == 0)
                    return new RecordReadResult(false, null, -1);
                int length;
                var result = TryReadRecordInternal(workItem, 0, out length, out record);
                if (!result)
                    return new RecordReadResult(false, null, -1);

                int nextLogicalPos;
                if (_isReadonly && _chunkFooter.MapSize > 0)
                {
                    if (_chunkFooter.MapSize > sizeof(ulong))
                        nextLogicalPos = ReadPosMap(workItem, 1).LogPos;
                    else
                        nextLogicalPos = (int)(record.Position % _chunkHeader.ChunkSize) + 4 + length;
                }
                else
                    nextLogicalPos = GetLogicalPosition(workItem);
                return new RecordReadResult(true, record, nextLogicalPos);

            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        public RecordReadResult TryReadSameOrClosest(int logicalPosition)
        {
            var workItem = GetReaderWorkItem();
            try
            {
                var pos = TranslateToSameOrClosestPosition(workItem, logicalPosition);
                var actualPosition = pos.Item1.ActualPos;
                if (actualPosition == -1 || actualPosition >= _actualDataSize)
                    return new RecordReadResult(false, null, -1);

                int length;
                LogRecord record;
                if (!TryReadRecordInternal(workItem, actualPosition, out length, out record))
                    return new RecordReadResult(false, null, -1);

                int nextLogicalPos;
                if (_isReadonly && _chunkFooter.MapSize > 0)
                {
                    if ((pos.Item2 + 1) * sizeof(ulong) < _chunkFooter.MapSize)
                        nextLogicalPos = ReadPosMap(workItem, pos.Item2 + 1).LogPos;
                    else
                        nextLogicalPos = (int)(record.Position % _chunkHeader.ChunkSize) + 4 + length;
                }
                else
                    nextLogicalPos = GetLogicalPosition(workItem);

                return new RecordReadResult(true, record, nextLogicalPos);
            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        private Tuple<PosMap, int> TranslateToSameOrClosestPosition(ReaderWorkItem workItem, int logicalPosition)
        {
            if (!_isReadonly || _chunkFooter.MapSize == 0)
                return Tuple.Create(new PosMap(logicalPosition, logicalPosition), -1);

            var midpoints = _midpoints;
            if (workItem.IsMemory || midpoints == null)
            {
                var mapCount = _chunkFooter.MapSize / sizeof(ulong);
                return TranslateSameOrClosestWithoutMidpoints(workItem, logicalPosition, 0, mapCount - 1);
            }
            else
                return TranslateSameOrClosestWithMidpoints(workItem, midpoints, logicalPosition);
        }

        private bool TryReadRecordInternal(ReaderWorkItem workItem, int actualPosition, out int length, out LogRecord record)
        {
            length = -1;
            record = null;

            workItem.Stream.Position = GetRealPosition(actualPosition, workItem.IsMemory);

            if (!VerifyStreamLength(workItem.Stream, 4))
                return false;
            
            length = workItem.Reader.ReadInt32();
            CheckLength(workItem, length, actualPosition);

            record = LogRecord.ReadFrom(workItem.Reader);
            return true;
        }

        private void CheckLength(ReaderWorkItem workItem, int length, int actualPosition)
        {
            if (length <= 0)
            {
                throw new ArgumentException(
                        string.Format("Log record at actual pos {0} has non-positive length: {1}. "
                                      + "Something is seriously wrong.",
                                      actualPosition,
                                      length));
            }
            if (length > TFConsts.MaxLogRecordSize)
            {
                throw new ArgumentException(
                        string.Format("Log record at actual pos {0} has too large length: {1} bytes, "
                                      + "while limit is {2} bytes.",
                                      actualPosition,
                                      length,
                                      TFConsts.MaxLogRecordSize));
            }
            if (!VerifyStreamLength(workItem.Stream, length))
                throw new UnableToReadPastEndOfStreamException();
        }

        private int TranslatePosition(ReaderWorkItem workItem, int pos)
        {
            if (!_isReadonly || _chunkFooter.MapSize == 0)
                return pos;

            var midpoints = _midpoints;
            if (workItem.IsMemory || midpoints == null)
            {
                var mapCount = _chunkFooter.MapSize/sizeof (ulong);
                return TranslateWithoutMidpoints(workItem, pos, 0, mapCount - 1);
            }
            else
                return TranslateWithMidpoints(workItem, midpoints, pos);
        }

        private int TranslateWithMidpoints(ReaderWorkItem workItem, Midpoint[] midpoints, int pos)
        {
            if (pos < midpoints[0].LogPos || pos > midpoints[midpoints.Length - 1].LogPos)
                return -1;

            var recordRange = LocatePosRange(midpoints, pos);
            return TranslateWithoutMidpoints(workItem, pos, recordRange.Item1, recordRange.Item2);
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
                int m = l + (r - l + 1)/2;
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
                int m = l + (r - l)/2;
                if (midpoints[m].LogPos >= pos)
                    r = m;
                else
                    l = m + 1;
            }
            return l;
        }

        private int TranslateWithoutMidpoints(ReaderWorkItem workItem, int pos, int startIndex, int endIndex)
        {
            int low = startIndex;
            int high = endIndex;
            while (low <= high)
            {
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

        private Tuple<PosMap, int> TranslateSameOrClosestWithMidpoints(ReaderWorkItem workItem, Midpoint[] midpoints, int pos)
        {
            if (pos > midpoints[midpoints.Length - 1].LogPos)
                return Tuple.Create(new PosMap(-1, -1), -1);

            var recordRange = LocatePosRange(midpoints, pos);
            return TranslateSameOrClosestWithoutMidpoints(workItem, pos, recordRange.Item1, recordRange.Item2);
        }

        private Tuple<PosMap, int> TranslateSameOrClosestWithoutMidpoints(ReaderWorkItem workItem, int pos, int startIndex, int endIndex)
        {
            PosMap res = ReadPosMap(workItem, endIndex);
            if (pos > res.LogPos)
                return Tuple.Create(new PosMap(-1, -1), -1);
            int low = startIndex;
            int high = endIndex;
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
            return Tuple.Create(res, high);
        }

        private static int GetRealPosition(int logicalPosition, bool inMemory)
        {
            return inMemory ? logicalPosition : ChunkHeader.Size + logicalPosition;
        }

        private int GetLogicalPosition(ReaderWorkItem workItem)
        {
            return workItem.IsMemory ? (int)workItem.Stream.Position : (int)workItem.Stream.Position - ChunkHeader.Size;
        }

        private int GetLogicalPosition(WriterWorkItem workItem)
        {
            return (int) workItem.Stream.Position - ChunkHeader.Size;
        }

        private bool VerifyStreamLength(Stream stream, int length)
        {
            // chunk header size is not included in _chunkSize value
            return length + stream.Position <= _chunkHeader.ChunkSize + ChunkHeader.Size;
        }

        public RecordWriteResult TryAppend(LogRecord record)
        {
            if (_isReadonly) 
                throw new InvalidOperationException("Cannot write to a read-only block.");

            var workItem = _writerWorkItem;
            var buffer = workItem.Buffer;
            var bufferWriter = workItem.BufferWriter;
            var stream = workItem.Stream;

            buffer.SetLength(4);
            buffer.Position = 4;
            record.WriteTo(bufferWriter);
            buffer.Position = 0;
            var toWriteLength = (int) buffer.Length;
            bufferWriter.Write(toWriteLength - 4);

            if (!VerifyStreamLength(stream, toWriteLength)) 
                return RecordWriteResult.Failed(GetLogicalPosition(workItem));

            var oldPosition = WriteRawData(buffer);
            _actualDataSize = GetLogicalPosition(workItem);
            return RecordWriteResult.Successful(oldPosition, _actualDataSize);
        }

        private long WriteRawData(MemoryStream buffer)
        {
            var len = (int) buffer.Length;
            var buf = buffer.GetBuffer();
            var curPos = GetLogicalPosition(_writerWorkItem);

            //MD5
            _writerWorkItem.MD5.TransformBlock(buf, 0, len, null, 0);
            //FILE
            _writerWorkItem.Stream.Write(buf, 0, len); // as we are always append-only, stream's position should be right here
            //MEMORY
            if (_writerWorkItem.UnmanagedMemoryStream != null)
            {
                var realMemPos = GetRealPosition(curPos, inMemory: true);
                _writerWorkItem.UnmanagedMemoryStream.Seek(realMemPos, SeekOrigin.Begin);
                _writerWorkItem.UnmanagedMemoryStream.Write(buf, 0, len);
            }
            return curPos;
        }

        public void Flush()
        {
            if (_isReadonly) 
                throw new InvalidOperationException("Cannot write to a read-only TFChunk.");
            
            _writerWorkItem.Stream.Flush(flushToDisk: true);
#if LESS_THAN_NET_4_0
            Win32.FlushFileBuffers(_fileStream.SafeFileHandle);
#endif
        }

        public void Complete()
        {
            if (_isReadonly)
                throw new InvalidOperationException("Cannot complete a read-only TFChunk.");

            _chunkFooter = WriteFooter(_chunkHeader.ChunkSize, null);
            Flush();
            _isReadonly = true;
            CloseWriterStream();
        }

        public void CompleteScavenge(ICollection<PosMap> mapping)
        {
            if (_isReadonly)
                throw new InvalidOperationException("Cannot complete a read-only TFChunk.");

            _chunkFooter = WriteFooter(_actualDataSize, mapping);
            Flush();
            _isReadonly = true;
            CloseWriterStream();
        }

        private ChunkFooter WriteFooter(int actualChunkSize, ICollection<PosMap> mapping)
        {
            int mapSize = 0;
            if (mapping != null)
            {
                if (_cached)
                {
                    throw new InvalidOperationException("Trying to write mapping while chunk is cached! "
                                                      + "You probably are writing scavenged chunk as cached. "
                                                      + "Don't do this!");
                }
                mapSize = mapping.Count * sizeof(ulong);
                _writerWorkItem.Buffer.SetLength(mapSize);
                _writerWorkItem.Buffer.Position = 0;
                foreach (var map in mapping)
                {
                    _writerWorkItem.BufferWriter.Write(map.AsUInt64());
                }
                WriteRawData(_writerWorkItem.Buffer);
            }

            var footer = new ChunkFooter(true,
                                         actualChunkSize,
                                         _actualDataSize,
                                         mapSize,
                                         new byte[ChunkFooter.ChecksumSize]);

            //MD5
            _writerWorkItem.MD5.TransformFinalBlock(footer.AsByteArray(), 0, ChunkFooter.Size - ChunkFooter.ChecksumSize);

            //FILE
            footer.MD5Hash = _writerWorkItem.MD5.Hash;
            var footerBytes = footer.AsByteArray();
            var footerPos = ChunkHeader.Size + actualChunkSize + mapSize;
            _writerWorkItem.Stream.Position = footerPos;
            _writerWorkItem.Stream.Write(footerBytes, 0, ChunkFooter.Size);

            var fileSize = ChunkHeader.Size + actualChunkSize + mapSize + ChunkFooter.Size;
            if (_writerWorkItem.Stream.Length != fileSize)
                _writerWorkItem.Stream.SetLength(fileSize);

            return footer;
        }

        private void CloseWriterStream()
        {
            if (_writerWorkItem == null)
                return;

            if (_writerWorkItem.Stream != null)
                _writerWorkItem.Stream.Dispose();

            var unmanagedStream = _writerWorkItem.UnmanagedMemoryStream;
            if (unmanagedStream != null)
            {
                unmanagedStream.Dispose();
                _writerWorkItem.UnmanagedMemoryStream = null;
            }
            _writerWorkItem = null;
        }

        public void Dispose()
        {
            _selfdestructin54321 = true;
            TryDestruct();
            TryDestructUnmanagedMemory();
        }

        public void MarkForDeletion()
        {
            _selfdestructin54321 = true;
            _deleteFile = true;
            TryDestruct();
            TryDestructUnmanagedMemory();
        }

        private int _destructedFileStreams;
        private int _destructedMemStreams;
        private int _lockedCount = 0;

        private void TryDestruct()
        {
            ReaderWorkItem workItem;
            var destructed = _destructedFileStreams;
            while (_streams.TryDequeue(out workItem))
            {
                destructed = Interlocked.Increment(ref _destructedFileStreams);
                workItem.Stream.Close();
                workItem.Stream.Dispose();
                
            }
            if (destructed == _maxReadThreads)
            {
                if (File.Exists(_filename))
                    File.SetAttributes(_filename, FileAttributes.Normal);

                if (_writerWorkItem != null)
                {
                    _writerWorkItem.Stream.Close();
                    _writerWorkItem.Stream.Dispose();
                    if (_writerWorkItem.UnmanagedMemoryStream != null)
                    {
                        _writerWorkItem.UnmanagedMemoryStream.Dispose();
                        _writerWorkItem.UnmanagedMemoryStream = null;
                    }
                }
                if (_deleteFile && _lockedCount == 0)
                    Helper.EatException(() => File.Delete(_filename));

                _destroyEvent.Set();
            }
            
        }

        private void TryDestructUnmanagedMemory()
        {
            var memStreams = _memoryStreams;

            ReaderWorkItem workItem;
            while (memStreams != null && memStreams.TryDequeue(out workItem))
            {
                var destructed = Interlocked.Increment(ref _destructedMemStreams);
                if (destructed == _maxReadThreads)
                    FreeCachedData();
            }
        }

        private void FreeCachedData()
        {
            if (_cachedData != null)
            {
                Marshal.FreeHGlobal((IntPtr)_cachedData);
                _cachedData = null;
            }
        }

        public void WaitForDestroy(int timeoutMs)
        {
            if (!_destroyEvent.WaitOne(timeoutMs))
                throw new TimeoutException();
        }

        private ReaderWorkItem GetReaderWorkItem()
        { 
            for (int i = 0; i < 10; i++)
            {
                if (_selfdestructin54321)
                    throw new FileBeingDeletedException();

                ReaderWorkItem item;

                // try get cached reader first
                var memstreams = _memoryStreams;
                if (_cached && memstreams != null && memstreams.TryDequeue(out item))
                    return item;

                if (_selfdestructin54321)
                    throw new FileBeingDeletedException();
                //there is an extremely unlikely race condition here 
                //GFY TODO resolve it though impact is minimal
                //as of now the worst thing thing that can happen is a chunk does not get deleted
                //but gets picked up later when the database gets restarted
                // if no cached reader - use usual
                if (_streams.TryDequeue(out item))
                    return item;
            }
            throw new Exception("Unable to acquire reader work item.");
        }

        private void ReturnReaderWorkItem(ReaderWorkItem item)
        {
            if (item.IsMemory)
            {
                _memoryStreams.Enqueue(item);
                if (!_cached || _selfdestructin54321)
                    TryDestructUnmanagedMemory();
            }
            else
            {
                _streams.Enqueue(item);
                if (_selfdestructin54321)
                    TryDestruct();
            }
        }

        public TFChunkBulkReader AcquireReader()
        {
            if(_selfdestructin54321)
            {
                throw new FileBeingDeletedException();
            }
            Interlocked.Increment(ref _lockedCount);
            return new TFChunkBulkReader(this);
        }

        internal void ReleaseReader(TFChunkBulkReader reader)
        {
            Interlocked.Decrement(ref _lockedCount);
            if(_selfdestructin54321)
            {
                TryDestruct();
            }
        }

        private struct Midpoint
        {
            public readonly int ItemIndex;
            public readonly int LogPos;

            public Midpoint(int itemIndex, PosMap posmap)
            {
                ItemIndex = itemIndex;
                LogPos = posmap.LogPos;
            }
        }

    }

    //TODO GFY ACTUALLY GET STREAM HERE AND USE IT. 
    public class TFChunkBulkReader : IDisposable
    {
        private readonly TFChunk _chunk;

        internal TFChunkBulkReader(TFChunk chunk)
        {
            _chunk = chunk;
        }

        public void Release()
        {
            //Kill bulk stream
            _chunk.ReleaseReader(this);
        }

        public void Dispose()
        {
            Release();
        }
    }

    public struct PosMap
    {
        public readonly int LogPos;
        public readonly int ActualPos;

        public PosMap(ulong posmap)
        {
            LogPos = (int) (posmap >> 32);
            ActualPos = (int) (posmap & 0xFFFFFFFF);
        }

        public PosMap(int logPos, int actualPos)
        {
            LogPos = logPos;
            ActualPos = actualPos;
        }

        public ulong AsUInt64()
        {
            return (((ulong)LogPos) << 32) | (uint)ActualPos;
        }
    }
}