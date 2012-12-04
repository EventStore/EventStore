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
    public unsafe class TFChunk : IDisposable
    {
        public const byte CurrentChunkVersion = 2;
        public const int WriteBufferSize = 4096;
        public const int ReadBufferSize = 512;

        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunk>();

        public bool IsReadOnly { get { return _isReadonly; } }
        public bool IsCached { get { return _cached; } }
        public int ActualDataSize { get { return _actualDataSize; } }
        public string FileName { get { return _filename; } }
        public int FileSize { get { return (int) new FileInfo(_filename).Length; } }

        public ChunkHeader ChunkHeader { get { return _chunkHeader; } }
        public ChunkFooter ChunkFooter { get { return _chunkFooter; } }

        public int PhysicalWriterPosition 
        {
            get
            {
                var writerWorkItem = _writerWorkItem;
                if (writerWorkItem == null)
                    throw new InvalidOperationException(string.Format("TFChunk {0} is not in write mode.", _filename));
                return (int) writerWorkItem.Stream.Position;
            }
        }

        private readonly string _filename;
        private volatile bool _isReadonly;
        private ChunkHeader _chunkHeader;
        private ChunkFooter _chunkFooter;
        
        private readonly int _maxReadThreads;
        private readonly Common.Concurrent.ConcurrentQueue<ReaderWorkItem> _streams = new Common.Concurrent.ConcurrentQueue<ReaderWorkItem>();
        private Common.Concurrent.ConcurrentQueue<ReaderWorkItem> _memoryStreams;
        private WriterWorkItem _writerWorkItem;
        private volatile int _actualDataSize;

        private byte* _cachedData;
        private int _cachedDataLength;
        private volatile bool _cached;

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

        private void InitCompleted(bool verifyHash)
        {
            if (!File.Exists(_filename))
                throw new CorruptDatabaseException(new ChunkNotFoundException(_filename));

            _isReadonly = true;
            CreateReaderStreams();

            var reader = GetReaderWorkItem();
            try
            {
                Debug.Assert(!reader.IsMemory);

                _chunkHeader = ReadHeader(reader.Stream);
                if (_chunkHeader.Version != CurrentChunkVersion)
                    throw new CorruptDatabaseException(new WrongTFChunkVersionException(_filename, _chunkHeader.Version, CurrentChunkVersion));
                _chunkFooter = ReadFooter(reader.Stream);
                _actualDataSize = _chunkFooter.ActualDataSize;

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
            if (verifyHash)
                VerifyFileHash();
        }

        private void InitNew(ChunkHeader chunkHeader, int fileSize)
        {
            Ensure.NotNull(chunkHeader, "chunkHeader");
            Ensure.Positive(fileSize, "fileSize");

            _isReadonly = false;
            _chunkHeader = chunkHeader;
            _actualDataSize = 0;

            CreateWriterWorkItemForNewChunk(chunkHeader, fileSize);
            CreateReaderStreams();

            SetAttributes();
        }

        private void InitOngoing(int writePosition, bool checkSize)
        {
            Ensure.Nonnegative(writePosition, "writePosition");
            if (!File.Exists(_filename))
                throw new CorruptDatabaseException(new ChunkNotFoundException(_filename));

            _isReadonly = false;
            _actualDataSize = writePosition;

            CreateWriterWorkItemForExistingChunk(writePosition, out _chunkHeader);
            if (_chunkHeader.Version != CurrentChunkVersion)
                throw new CorruptDatabaseException(new WrongTFChunkVersionException(_filename, _chunkHeader.Version, CurrentChunkVersion));
            CreateReaderStreams();

            if (checkSize)
            {
                var expectedFileSize = _chunkHeader.ChunkSize + ChunkHeader.Size + ChunkFooter.Size;
                if (_writerWorkItem.Stream.Length != expectedFileSize)
                {
                    throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                        string.Format("Chunk file '{0}' should have file size {1} bytes, but instead has {2} bytes length.",
                                      _filename,
                                      expectedFileSize,
                                      _writerWorkItem.Stream.Length)));
                }
            }

            SetAttributes();
        }

        public static TFChunk FromCompletedFile(string filename, bool verifyHash)
        {
            var chunk = new TFChunk(filename, TFConsts.TFChunkReaderCount, TFConsts.MidpointsDepth);
            try
            {
                chunk.InitCompleted(verifyHash);
            }
            catch
            {
                chunk.Dispose();
                throw;
            }
            return chunk;
        }

        public static TFChunk FromOngoingFile(string filename, int writePosition, bool checkSize)
        {
            var chunk = new TFChunk(filename, TFConsts.TFChunkReaderCount, TFConsts.MidpointsDepth);
            try
            {
                chunk.InitOngoing(writePosition, checkSize);
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
            var chunkHeader = new ChunkHeader(CurrentChunkVersion, chunkSize, chunkNumber, chunkNumber, chunkScavengeVersion);
            return CreateWithHeader(filename, chunkHeader, chunkSize + ChunkHeader.Size + ChunkFooter.Size);
        }

        public static TFChunk CreateWithHeader(string filename, ChunkHeader header, int fileSize)
        {
            var chunk = new TFChunk(filename, TFConsts.TFChunkReaderCount, TFConsts.MidpointsDepth);
            try
            {
                chunk.InitNew(header, fileSize);
            }
            catch
            {
                chunk.Dispose();
                throw;
            }
            return chunk;
        }

        private Stream GetSequentialReaderFileStream()
        {
            return new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                                   64000, FileOptions.SequentialScan);
            
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

        private void CreateWriterWorkItemForNewChunk(ChunkHeader chunkHeader, int fileSize)
        {
            var md5 = MD5.Create();
            var stream = new FileStream(_filename, FileMode.Create, FileAccess.ReadWrite, FileShare.Read,
                                        WriteBufferSize, FileOptions.SequentialScan);
            var writer = new BinaryWriter(stream);
            stream.SetLength(fileSize);
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
                File.SetAttributes(_filename, FileAttributes.NotContentIndexed);
            });
        }

        public void VerifyFileHash()
        {
            if (!IsReadOnly)
                throw new InvalidOperationException("You can't verify hash of not-completed TFChunk.");

            Log.Trace("Verifying hash for TFChunk '{0}'...", _filename);

            var workItem = GetReaderWorkItem();
            try
            {
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
                var mapCount = _chunkFooter.MapCount;
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

                for (int x = 0, i = 0, xN = mapCount - 1; 
                     x < xN; 
                     x += segmentSize, i += 1)
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
                _cachedDataLength = (int) workItem.Stream.Length - ChunkHeader.Size; // all except header
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

        private Common.Concurrent.ConcurrentQueue<ReaderWorkItem> BuildCacheReaders()
        {
            var queue = new Common.Concurrent.ConcurrentQueue<ReaderWorkItem>();
            for (int i = 0; i < _maxReadThreads; i++)
            {
                var stream = new UnmanagedMemoryStream(_cachedData, _cachedDataLength);
                var reader = new BinaryReader(stream);
                queue.Enqueue(new ReaderWorkItem(stream, reader, isMemory: true));
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

        public RecordReadResult TryReadAt(int logicalPosition)
        {
            var workItem = GetReaderWorkItem();
            try
            {
                LogRecord record;
                var actualPosition = TranslateExactPosition(workItem, logicalPosition);
                if (actualPosition == -1 || actualPosition >= _actualDataSize)
                    return RecordReadResult.Failure;
                int length;
                var result = TryReadForwardInternal(workItem, actualPosition, out length, out record);
                return new RecordReadResult(result, -1, record, length);
            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        private int TranslateExactPosition(ReaderWorkItem workItem, int pos)
        {
            if (!_isReadonly || _chunkFooter.MapSize == 0)
                return pos;

            var midpoints = _midpoints;
            if (workItem.IsMemory || midpoints == null)
            {
                var mapCount = _chunkFooter.MapSize / sizeof(ulong);
                return TranslateExactWithoutMidpoints(workItem, pos, 0, mapCount - 1);
            }
            return TranslateExactWithMidpoints(workItem, midpoints, pos);
        }

        private int TranslateExactWithMidpoints(ReaderWorkItem workItem, Midpoint[] midpoints, int pos)
        {
            if (pos < midpoints[0].LogPos || pos > midpoints[midpoints.Length - 1].LogPos)
                return -1;

            var recordRange = LocatePosRange(midpoints, pos);
            return TranslateExactWithoutMidpoints(workItem, pos, recordRange.Item1, recordRange.Item2);
        }

        private int TranslateExactWithoutMidpoints(ReaderWorkItem workItem, int pos, int startIndex, int endIndex)
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

        public RecordReadResult TryReadFirst()
        {
            var workItem = GetReaderWorkItem();
            try
            {
                LogRecord record;
                if (_actualDataSize == 0)
                    return RecordReadResult.Failure;
                int length;
                var result = TryReadForwardInternal(workItem, 0, out length, out record);
                if (!result)
                    return RecordReadResult.Failure;

                int nextLogicalPos;
                if (_isReadonly && _chunkFooter.MapSize > 0)
                {
                    if (_chunkFooter.MapSize > sizeof(ulong))
                        nextLogicalPos = ReadPosMap(workItem, 1).LogPos;
                    else
                        nextLogicalPos = (int)(record.Position % _chunkHeader.ChunkSize) + length + 2*sizeof(int);
                }
                else
                    nextLogicalPos = 0 + length + 2*sizeof(int);
                return new RecordReadResult(true, nextLogicalPos, record, length);

            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        public RecordReadResult TryReadClosestForward(int logicalPosition)
        {
            var workItem = GetReaderWorkItem();
            try
            {
                var pos = TranslateClosestForwardPosition(workItem, logicalPosition);
                var actualPosition = pos.Item1;
                if (actualPosition == -1 || actualPosition >= _actualDataSize)
                    return RecordReadResult.Failure;

                int length;
                LogRecord record;
                if (!TryReadForwardInternal(workItem, actualPosition, out length, out record))
                    return RecordReadResult.Failure;

                int nextLogicalPos;
                if (_isReadonly && _chunkFooter.MapSize > 0)
                {
                    if (pos.Item2 + 1 < _chunkFooter.MapCount)
                        nextLogicalPos = ReadPosMap(workItem, pos.Item2 + 1).LogPos;
                    else
                        nextLogicalPos = (int)(record.Position % _chunkHeader.ChunkSize) + length + 2*sizeof(int);
                }
                else
                    nextLogicalPos = actualPosition + length + 2*sizeof(int);

                return new RecordReadResult(true, nextLogicalPos, record, length);
            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        private bool TryReadForwardInternal(ReaderWorkItem workItem, int actualPosition, out int length, out LogRecord record)
        {
            length = -1;
            record = null;

            workItem.Stream.Position = GetRealPosition(actualPosition, workItem.IsMemory);

            if (!VerifyDataLengthForward(workItem, 2*sizeof(int)))
                return false;
            
            length = workItem.Reader.ReadInt32();

            if (length <= 0)
            {
                throw new ArgumentException(
                        string.Format("Log record at actual pos {0} has non-positive length: {1}. "
                                      + "Something is seriously wrong in chunk {2}-{3} ({4}).",
                                      actualPosition,
                                      length,
                                      _chunkHeader.ChunkStartNumber,
                                      _chunkHeader.ChunkEndNumber,
                                      _filename));
            }
            if (length > TFConsts.MaxLogRecordSize)
            {
                throw new ArgumentException(
                        string.Format("Log record at actual pos {0} has too large length: {1} bytes, "
                                      + "while limit is {2} bytes. Something is seriously wrong in chunk {3}-{4} ({5}).",
                                      actualPosition,
                                      length,
                                      TFConsts.MaxLogRecordSize,
                                      _chunkHeader.ChunkStartNumber,
                                      _chunkHeader.ChunkEndNumber,
                                      _filename));
            }

            if (!VerifyDataLengthForward(workItem, length + sizeof(int) /*suffix*/))
                throw new UnableToReadPastEndOfStreamException();

            record = LogRecord.ReadFrom(workItem.Reader);
           
            Debug.Assert(workItem.Reader.ReadInt32() == length); // verify suffix length == prefix length

            return true;
        }

        public RecordReadResult TryReadLast()
        {
            var workItem = GetReaderWorkItem();
            try
            {
                LogRecord record;
                if (_actualDataSize == 0)
                    return RecordReadResult.Failure;
                
                int length;
                var result = TryReadBackwardInternal(workItem, _actualDataSize, out length, out record);
                if (!result)
                    return RecordReadResult.Failure;

                int nextLogicalPos;
                if (_isReadonly && _chunkFooter.MapSize > 0)
                {
                    var mapCount = _chunkFooter.MapSize / sizeof(ulong);
                    nextLogicalPos = mapCount > 1
                                             ? ReadPosMap(workItem, mapCount - 1).LogPos
                                             : (int) (record.Position % _chunkHeader.ChunkSize);
                }
                else
                    nextLogicalPos = _actualDataSize - length - 2*sizeof(int);
                return new RecordReadResult(true, nextLogicalPos, record, length);
            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        public RecordReadResult TryReadClosestBackward(int logicalPosition)
        {
            var workItem = GetReaderWorkItem();
            try
            {
                var pos = TranslateClosestForwardPosition(workItem, logicalPosition);
                var actualPosition = pos.Item1;
                // here we allow actualPosition == _actualDataSize as we can read backward the very last record that way
                if (actualPosition == -1 || actualPosition > _actualDataSize) 
                    return RecordReadResult.Failure;

                int length;
                LogRecord record;
                if (!TryReadBackwardInternal(workItem, actualPosition, out length, out record))
                    return RecordReadResult.Failure;

                int nextLogicalPos;
                if (_isReadonly && _chunkFooter.MapSize > 0)
                {
                    nextLogicalPos = pos.Item2 > 0
                                             ? ReadPosMap(workItem, pos.Item2 - 1).LogPos
                                             : (int) (record.Position % _chunkHeader.ChunkSize);
                }
                else
                    nextLogicalPos = actualPosition - length - 2*sizeof(int);

                return new RecordReadResult(true, nextLogicalPos, record, length);
            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        private static bool TryReadBackwardInternal(ReaderWorkItem workItem, int actualPosition, out int length, out LogRecord record)
        {
            length = -1;
            record = null;

            if (actualPosition < 2 * sizeof(int)) // no space even for length prefix and suffix 
                return false;

            var realPos = GetRealPosition(actualPosition, workItem.IsMemory);
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
                throw new UnableToReadPastEndOfStreamException();

            workItem.Stream.Position = realPos - length - sizeof(int);
            record = LogRecord.ReadFrom(workItem.Reader);

#if DEBUG
            workItem.Stream.Position = realPos - length - 2*sizeof(int);
            var prefixLength = workItem.Reader.ReadInt32();
            Debug.Assert(prefixLength == length);
#endif
            return true;
        }

        private Tuple<int, int> TranslateClosestForwardPosition(ReaderWorkItem workItem, int logicalPosition)
        {
            if (!_isReadonly || _chunkFooter.MapSize == 0)
            {
                // this is mostly for ability to read closest backward from the very end
                var logicalPos = Math.Min(_actualDataSize, logicalPosition); 
                return Tuple.Create(logicalPos, -1);
            }

            var midpoints = _midpoints;
            if (workItem.IsMemory || midpoints == null)
            {
                var mapCount = _chunkFooter.MapSize / sizeof(ulong);
                return TranslateClosestForwardWithoutMidpoints(workItem, logicalPosition, 0, mapCount - 1);
            }
            return TranslateClosestForwardWithMidpoints(workItem, midpoints, logicalPosition);
        }

        private Tuple<int, int> TranslateClosestForwardWithMidpoints(ReaderWorkItem workItem, Midpoint[] midpoints, int pos)
        {
            if (pos > midpoints[midpoints.Length - 1].LogPos)
                return Tuple.Create(_actualDataSize, midpoints.Length); // to allow backward reading of the last record, forward read will decline anyway

            var recordRange = LocatePosRange(midpoints, pos);
            return TranslateClosestForwardWithoutMidpoints(workItem, pos, recordRange.Item1, recordRange.Item2);
        }

        private Tuple<int, int> TranslateClosestForwardWithoutMidpoints(ReaderWorkItem workItem, int pos, int startIndex, int endIndex)
        {
            PosMap res = ReadPosMap(workItem, endIndex);

            if (pos > res.LogPos)
                return Tuple.Create(_actualDataSize, endIndex + 1); // to allow backward reading of the last record, forward read will decline anyway
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

        private static int GetRealPosition(int logicalPosition, bool inMemory)
        {
            return inMemory ? logicalPosition : ChunkHeader.Size + logicalPosition;
        }

        private static int GetLogicalPosition(ReaderWorkItem workItem)
        {
            return workItem.IsMemory ? (int)workItem.Stream.Position : (int)workItem.Stream.Position - ChunkHeader.Size;
        }

        private static int GetLogicalPosition(WriterWorkItem workItem)
        {
            return (int) workItem.Stream.Position - ChunkHeader.Size;
        }

        private bool VerifyDataLengthForward(ReaderWorkItem workItem, int length)
        {
            var chunkSize = _isReadonly ? _chunkFooter.ActualDataSize : _chunkHeader.ChunkSize;
            return GetLogicalPosition(workItem) + length <= chunkSize;
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
            var length = (int) buffer.Length - 4;
            bufferWriter.Write(length); // length suffix
            buffer.Position = 0;
            bufferWriter.Write(length); // length prefix

            if (stream.Position + length + 8 > ChunkHeader.Size + _chunkHeader.ChunkSize) 
                return RecordWriteResult.Failed(GetLogicalPosition(workItem));

            var oldPosition = WriteRawData(buffer);
            _actualDataSize = GetLogicalPosition(workItem);
            return RecordWriteResult.Successful(oldPosition, _actualDataSize);
        }

        public bool TryAppendRawData(byte[] buffer)
        {
            var workItem = _writerWorkItem;
            var stream = workItem.Stream;
            if (stream.Position + buffer.Length > stream.Length)
                return false;
            WriteRawData(buffer, buffer.Length);
            return true;
        }

        private long WriteRawData(MemoryStream buffer)
        {
            var len = (int) buffer.Length;
            var buf = buffer.GetBuffer();
            return WriteRawData(buf, len);
        }

        private long WriteRawData(byte[] buf, int len)
        {
            var curPos = GetLogicalPosition(_writerWorkItem);

            //MD5
            _writerWorkItem.MD5.TransformBlock(buf, 0, len, null, 0);
            //FILE
            _writerWorkItem.Stream.Write(buf, 0, len); // as we are always append-only, stream's position should be right here
            //MEMORY
            var memStream = _writerWorkItem.UnmanagedMemoryStream;
            if (memStream != null)
            {
                var realMemPos = GetRealPosition(curPos, inMemory: true);
                memStream.Seek(realMemPos, SeekOrigin.Begin);
                memStream.Write(buf, 0, len);
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

            _chunkFooter = WriteFooter(null);
            Flush();
            _isReadonly = true;
            CloseWriterStream();
        }

        public void CompleteScavenge(ICollection<PosMap> mapping)
        {
            if (_isReadonly)
                throw new InvalidOperationException("Cannot complete a read-only TFChunk.");

            _chunkFooter = WriteFooter(mapping);
            Flush();
            _isReadonly = true;
            CloseWriterStream();
        }

        public void CompleteRaw()
        {
            if (_isReadonly)
                throw new InvalidOperationException("Cannot complete a read-only TFChunk.");
            if (_writerWorkItem.Stream.Position != _writerWorkItem.Stream.Length)
                throw new InvalidOperationException("The raw chunk is not completely written.");
            Flush();
            _chunkFooter = ReadFooter(_writerWorkItem.Stream);
            _isReadonly = true;
            CloseWriterStream();
        }

        private ChunkFooter WriteFooter(ICollection<PosMap> mapping)
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
                                         _actualDataSize,
                                         _actualDataSize,
                                         mapSize,
                                         new byte[ChunkFooter.ChecksumSize]);

            //MD5
            _writerWorkItem.MD5.TransformFinalBlock(footer.AsByteArray(), 0, ChunkFooter.Size - ChunkFooter.ChecksumSize);

            //FILE
            footer.MD5Hash = _writerWorkItem.MD5.Hash;
            var footerBytes = footer.AsByteArray();
            //var footerPos = ChunkHeader.Size + _actualDataSize + mapSize;
            //_writerWorkItem.Stream.Position = footerPos;
            _writerWorkItem.Stream.Write(footerBytes, 0, ChunkFooter.Size);

            var fileSize = ChunkHeader.Size + _actualDataSize + mapSize + ChunkFooter.Size;
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
        private volatile int _destructedMemStreams;
        private volatile int _lockedCount;

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
                {
                    Log.Info("File {0} has been marked for delete and will be deleted in TryDestruct.", _filename);
                    Helper.EatException(() => File.Delete(_filename));
                }

                _destroyEvent.Set();
            }
            
        }

        private void TryDestructUnmanagedMemory()
        {
            var memStreams = _memoryStreams;

            ReaderWorkItem workItem;
            while (memStreams != null && memStreams.TryDequeue(out workItem))
            {
#pragma warning disable 420
                var destructed = Interlocked.Increment(ref _destructedMemStreams);
#pragma warning restore 420
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
            if (_selfdestructin54321)
                throw new FileBeingDeletedException();
#pragma warning disable 420
            Interlocked.Increment(ref _lockedCount);
#pragma warning restore 420
            return new TFChunkBulkReader(this, GetSequentialReaderFileStream());
        }

        public void ReleaseReader(TFChunkBulkReader reader)
        {
#pragma warning disable 420
            Interlocked.Decrement(ref _lockedCount);
#pragma warning restore 420
            if (_selfdestructin54321)
                TryDestruct();
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

            public override string ToString()
            {
                return string.Format("ItemIndex: {0}, LogPos: {1}", ItemIndex, LogPos);
            }
        }
    }

    public struct BulkReadResult
    {
        public readonly int OldPosition;
        public readonly int BytesRead;
        public readonly bool IsEOF;

        public BulkReadResult(int oldPosition, int bytesRead, bool isEof)
        {
            OldPosition = oldPosition;
            BytesRead = bytesRead;
            IsEOF = isEof;
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

        public override string ToString()
        {
            return string.Format("LogPos: {0}, ActualPos: {1}", LogPos, ActualPos);
        }
    }
}