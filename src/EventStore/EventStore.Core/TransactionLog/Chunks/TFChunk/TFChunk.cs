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

namespace EventStore.Core.TransactionLog.Chunks.TFChunk
{
    public unsafe partial class TFChunk : IDisposable
    {
        public const byte CurrentChunkVersion = 2;
        public const int WriteBufferSize = 4096;
        public const int ReadBufferSize = 512;

        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunk>();

        public bool IsReadOnly { get { return _isReadOnly; } }
        public bool IsCached { get { return _cached; } }
        public int ActualDataSize { get { return _actualDataSize; } }
        public string FileName { get { return _filename; } }
        public int FileSize { get { return (int) new FileInfo(_filename).Length; } }

        public ChunkHeader ChunkHeader { get { return _chunkHeader; } }
        public ChunkFooter ChunkFooter { get { return _chunkFooter; } }
        public readonly int MidpointsDepth;

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
        private volatile bool _isReadOnly;
        private ChunkHeader _chunkHeader;
        private ChunkFooter _chunkFooter;
        
        private readonly int _maxReadThreads;
        private readonly Common.Concurrent.ConcurrentQueue<ReaderWorkItem> _fileStreams = new Common.Concurrent.ConcurrentQueue<ReaderWorkItem>();
        private readonly Common.Concurrent.ConcurrentQueue<ReaderWorkItem> _memStreams = new Common.Concurrent.ConcurrentQueue<ReaderWorkItem>();
        private volatile int _fileStreamCount;
        private int _memStreamCount;

        private WriterWorkItem _writerWorkItem;
        private volatile int _actualDataSize;

        private volatile IntPtr _cachedData;
        private int _cachedLength;
        private volatile bool _cached;

        private readonly ManualResetEventSlim _destroyEvent = new ManualResetEventSlim(false);
        private volatile bool _selfdestructin54321;
        private volatile bool _deleteFile;

        private IChunkReadSide _readSide;

        private TFChunk(string filename, int maxReadThreads, int midpointsDepth)
        {
            Ensure.NotNullOrEmpty(filename, "filename");
            Ensure.Positive(maxReadThreads, "maxReadThreads");
            Ensure.Nonnegative(midpointsDepth, "midpointsDepth");

            _filename = filename;
            _maxReadThreads = maxReadThreads;
            MidpointsDepth = midpointsDepth;
        }

        ~TFChunk()
        {
            FreeCachedData();
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

        public static TFChunk CreateNew(string filename, int chunkSize, int chunkStartNumber, int chunkEndNumber, bool isScavenged)
        {
            var chunkHeader = new ChunkHeader(CurrentChunkVersion, chunkSize, chunkStartNumber, chunkEndNumber, isScavenged);
            return CreateWithHeader(filename, chunkHeader, chunkSize + ChunkHeader.Size + ChunkFooter.Size);
        }

        public static TFChunk CreateNew(string filename, int chunkSize, int chunkNumber, bool isScavenged)
        {
            var chunkHeader = new ChunkHeader(CurrentChunkVersion, chunkSize, chunkNumber, chunkNumber, isScavenged);
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

        private void InitCompleted(bool verifyHash)
        {
            if (!File.Exists(_filename))
                throw new CorruptDatabaseException(new ChunkNotFoundException(_filename));

            _isReadOnly = true;
            CreateReaderStreams();

            var reader = GetReaderWorkItem();
            try
            {
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
            }
            finally
            {
                ReturnReaderWorkItem(reader);
            }

            _readSide = _chunkFooter.MapCount > 0 ? (IChunkReadSide) new TFChunkReadSideScavenged(this) : new TFChunkReadSideUnscavenged(this);
            _readSide.Cache();

            SetAttributes();
            if (verifyHash)
                VerifyFileHash();
        }

        private void InitNew(ChunkHeader chunkHeader, int fileSize)
        {
            Ensure.NotNull(chunkHeader, "chunkHeader");
            Ensure.Positive(fileSize, "fileSize");

            _isReadOnly = false;
            _chunkHeader = chunkHeader;
            _actualDataSize = 0;

            CreateWriterWorkItemForNewChunk(chunkHeader, fileSize);
            CreateReaderStreams();

            _readSide = new TFChunkReadSideUnscavenged(this);

            SetAttributes();
        }

        private void InitOngoing(int writePosition, bool checkSize)
        {
            Ensure.Nonnegative(writePosition, "writePosition");
            if (!File.Exists(_filename))
                throw new CorruptDatabaseException(new ChunkNotFoundException(_filename));

            _isReadOnly = false;
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
            _readSide = new TFChunkReadSideUnscavenged(this);
            SetAttributes();
        }

        private void CreateReaderStreams()
        {
#pragma warning disable 420
            Interlocked.Add(ref _fileStreamCount, _maxReadThreads);
#pragma warning restore 420
            for (int i = 0; i < _maxReadThreads; i++)
            {
                var stream = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                                            ReadBufferSize, FileOptions.RandomAccess);
                var reader = new BinaryReader(stream);
                _fileStreams.Enqueue(new ReaderWorkItem(stream, reader, false));
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
            var realPosition = GetRealPosition(writePosition);
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
                File.SetAttributes(_filename, FileAttributes.Temporary); // helps to tell to OS to cache in memory
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
                var footer = _chunkFooter;

                byte[] hash;
                using (var md5 = MD5.Create())
                {
                    workItem.Stream.Seek(0, SeekOrigin.Begin);
                    // hash header and data
                    MD5Hash.ContinuousHashFor(md5, workItem.Stream, 0, ChunkHeader.Size + footer.ActualDataSize);
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

        private static int GetRealPosition(int logicalPosition)
        {
            return ChunkHeader.Size + logicalPosition;
        }

        private static int GetLogicalPosition(WriterWorkItem workItem)
        {
            return (int)workItem.Stream.Position - ChunkHeader.Size;
        }

        public void CacheInMemory()
        {
            if (_cached || _selfdestructin54321)
                return;

            var sw = Stopwatch.StartNew();

            BuildCacheArray();
            BuildCacheReaders();

            var writerWorkItem = _writerWorkItem;
            if (writerWorkItem != null)
                writerWorkItem.UnmanagedMemoryStream = new UnmanagedMemoryStream((byte*)_cachedData, _cachedLength, _cachedLength, FileAccess.ReadWrite);
            
            _cached = true;
            _readSide.Uncache();

            Log.Trace("CACHED TFChunk #{0} at {1} in {2}.", _chunkHeader.ChunkStartNumber, Path.GetFileName(_filename), sw.Elapsed);
        }

        private void BuildCacheArray()
        {
            var workItem = GetReaderWorkItem();
            try
            {
                if (workItem.IsMemory)
                    throw new InvalidOperationException("When trying to build cache, reader worker is already in-memory reader.");

                _cachedLength = (int)workItem.Stream.Length;
                var cachedData = Marshal.AllocHGlobal(_cachedLength);
                try
                {
                    using (var unmanagedStream = new UnmanagedMemoryStream((byte*)cachedData, _cachedLength, _cachedLength, FileAccess.ReadWrite))
                    {
                        workItem.Stream.Seek(0, SeekOrigin.Begin);
                        workItem.Stream.CopyTo(unmanagedStream);
                    }
                }
                catch
                {
                    Marshal.FreeHGlobal(cachedData);
                    throw;
                }
                _cachedData = cachedData;
            }
            finally
            {
                ReturnReaderWorkItem(workItem);
            }
        }

        private void BuildCacheReaders()
        {
            Interlocked.Add(ref _memStreamCount, _maxReadThreads);
            for (int i = 0; i < _maxReadThreads; i++)
            {
                var stream = new UnmanagedMemoryStream((byte*)_cachedData, _cachedLength);
                var reader = new BinaryReader(stream);
                _memStreams.Enqueue(new ReaderWorkItem(stream, reader, isMemory: true));
            }
        }

        public void UnCacheFromMemory()
        {
            var wasCached = _cached;

            if (!_selfdestructin54321)
                _readSide.Cache();

            _cached = false;

            var writerWorkItem = _writerWorkItem;
            var unmanagedMemStream = writerWorkItem == null ? null : writerWorkItem.UnmanagedMemoryStream;
            if (unmanagedMemStream != null)
            {
                unmanagedMemStream.Dispose();
                writerWorkItem.UnmanagedMemoryStream = null;
            }

            TryDestructMemStreams();

            if (wasCached)
                Log.Trace("UNCACHED TFChunk #{0} at {1}", _chunkHeader.ChunkStartNumber, Path.GetFileName(_filename));
        }

        public RecordReadResult TryReadAt(int logicalPosition)
        {
            return _readSide.TryReadAt(logicalPosition);
        }

        public RecordReadResult TryReadFirst()
        {
            return _readSide.TryReadFirst();
        }

        public RecordReadResult TryReadClosestForward(int logicalPosition)
        {
            return _readSide.TryReadClosestForward(logicalPosition);
        }

        public RecordReadResult TryReadLast()
        {
            return _readSide.TryReadLast();
        }

        public RecordReadResult TryReadClosestBackward(int logicalPosition)
        {
            return _readSide.TryReadClosestBackward(logicalPosition);
        }

        public RecordWriteResult TryAppend(LogRecord record)
        {
            if (_isReadOnly) 
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

            if (stream.Position + length + 2*sizeof(int) > ChunkHeader.Size + _chunkHeader.ChunkSize) 
                return RecordWriteResult.Failed(GetLogicalPosition(workItem));

            var oldPosition = WriteRawData(workItem, buffer);
            _actualDataSize = GetLogicalPosition(workItem);
            return RecordWriteResult.Successful(oldPosition, _actualDataSize);
        }

        public bool TryAppendRawData(byte[] buffer)
        {
            var workItem = _writerWorkItem;
            if (workItem.Stream.Position + buffer.Length > workItem.Stream.Length)
                return false;
            WriteRawData(workItem, buffer, buffer.Length);
            return true;
        }

        private static long WriteRawData(WriterWorkItem workItem, MemoryStream buffer)
        {
            var len = (int) buffer.Length;
            var buf = buffer.GetBuffer();
            return WriteRawData(workItem, buf, len);
        }

        private static long WriteRawData(WriterWorkItem workItem, byte[] buf, int len)
        {
            var curPos = GetLogicalPosition(workItem);

            //MD5
            workItem.MD5.TransformBlock(buf, 0, len, null, 0);
            //FILE
            workItem.Stream.Write(buf, 0, len); // as we are always append-only, stream's position should be right here
            //MEMORY
            var memStream = workItem.UnmanagedMemoryStream;
            if (memStream != null)
            {
                var realMemPos = GetRealPosition(curPos);
                memStream.Seek(realMemPos, SeekOrigin.Begin);
                memStream.Write(buf, 0, len);
            }
            return curPos;
        }

        public void Flush()
        {
            if (_isReadOnly) 
                throw new InvalidOperationException("Cannot write to a read-only TFChunk.");
            
            _writerWorkItem.Stream.Flush(flushToDisk: true);
#if LESS_THAN_NET_4_0
            Win32.FlushFileBuffers(_fileStream.SafeFileHandle);
#endif
        }

        public void Complete()
        {
            CompleteScavenge(null);
        }

        public void CompleteScavenge(ICollection<PosMap> mapping)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("Cannot complete a read-only TFChunk.");

            _chunkFooter = WriteFooter(mapping);
            Flush();
            _isReadOnly = true;

            CleanUpWriterWorkItem(_writerWorkItem);
            _writerWorkItem = null;
        }

        public void CompleteRaw()
        {
            if (_isReadOnly)
                throw new InvalidOperationException("Cannot complete a read-only TFChunk.");
            if (_writerWorkItem.Stream.Position != _writerWorkItem.Stream.Length)
                throw new InvalidOperationException("The raw chunk is not completely written.");
            Flush();
            _chunkFooter = ReadFooter(_writerWorkItem.Stream);
            _isReadOnly = true;

            CleanUpWriterWorkItem(_writerWorkItem);
            _writerWorkItem = null;
        }

        private ChunkFooter WriteFooter(ICollection<PosMap> mapping)
        {
            var workItem = _writerWorkItem;

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
                workItem.Buffer.SetLength(mapSize);
                workItem.Buffer.Position = 0;
                foreach (var map in mapping)
                {
                    workItem.BufferWriter.Write(map.AsUInt64());
                }
                WriteRawData(workItem, workItem.Buffer);
            }

            var footerNoHash = new ChunkFooter(true, _actualDataSize, _actualDataSize, mapSize, new byte[ChunkFooter.ChecksumSize]);
            //MD5
            workItem.MD5.TransformFinalBlock(footerNoHash.AsByteArray(), 0, ChunkFooter.Size - ChunkFooter.ChecksumSize);
            //FILE
            var footerWithHash = new ChunkFooter(true, _actualDataSize, _actualDataSize, mapSize, workItem.MD5.Hash);
            workItem.Stream.Write(footerWithHash.AsByteArray(), 0, ChunkFooter.Size);

            var fileSize = ChunkHeader.Size + _actualDataSize + mapSize + ChunkFooter.Size;
            if (workItem.Stream.Length != fileSize)
                workItem.Stream.SetLength(fileSize);

            return footerWithHash;
        }

        private void CleanUpWriterWorkItem(WriterWorkItem writerWorkItem)
        {
            if (writerWorkItem == null)
                return;

            if (writerWorkItem.Stream != null)
                writerWorkItem.Stream.Dispose();

            var unmanagedStream = writerWorkItem.UnmanagedMemoryStream;
            if (unmanagedStream != null)
            {
                unmanagedStream.Dispose();
                writerWorkItem.UnmanagedMemoryStream = null;
            }
        }

        public void Dispose()
        {
            _selfdestructin54321 = true;
            TryDestructFileStreams();
            TryDestructMemStreams();
        }

        public void MarkForDeletion()
        {
            _selfdestructin54321 = true;
            _deleteFile = true;
            TryDestructFileStreams();
            TryDestructMemStreams();
        }

        private void TryDestructFileStreams()
        {
            int fileStreamCount = int.MaxValue;

            ReaderWorkItem workItem;
            while (_fileStreams.TryDequeue(out workItem))
            {
                workItem.Stream.Dispose();
#pragma warning disable 420
                fileStreamCount = Interlocked.Decrement(ref _fileStreamCount);
#pragma warning restore 420
            }

            Debug.Assert(fileStreamCount >= 0, "Somehow we managed to decrease count of file streams below zero.");
            if (fileStreamCount == 0) // we are the last who should "turn the light off" for file streams
                CleanUpFileStreamDestruction();
        }

        private void CleanUpFileStreamDestruction()
        {
            CleanUpWriterWorkItem(_writerWorkItem);

            Helper.EatException(() => File.SetAttributes(_filename, FileAttributes.Normal));

            if (_deleteFile)
            {
                Log.Info("File {0} has been marked for delete and will be deleted in TryDestructFileStreams.", _filename);
                Helper.EatException(() => File.Delete(_filename));
            }

            _destroyEvent.Set();
        }

        private void TryDestructMemStreams()
        {
            int memStreamCount = int.MaxValue;

            ReaderWorkItem workItem;
            while (_memStreams.TryDequeue(out workItem))
            {
                memStreamCount = Interlocked.Decrement(ref _memStreamCount);
            }
            Debug.Assert(memStreamCount >= 0, "Somehow we managed to decrease count of memory streams below zero.");
            if (memStreamCount == 0) // we are the last who should "turn the light off" for memory streams
                FreeCachedData();
        }

        private void FreeCachedData()
        {
#pragma warning disable 420
            var cachedData = Interlocked.Exchange(ref _cachedData, IntPtr.Zero);
#pragma warning restore 420
            if (cachedData != IntPtr.Zero)
                Marshal.FreeHGlobal(cachedData);
        }

        public void WaitForDestroy(int timeoutMs)
        {
            if (!_destroyEvent.Wait(timeoutMs))
                throw new TimeoutException();
        }

        private ReaderWorkItem GetReaderWorkItem()
        { 
            for (int i = 0; i < 10; i++)
            {
                ReaderWorkItem item;

                // try get memory stream reader first
                if (_memStreams.TryDequeue(out item))
                    return item;
                if (_fileStreams.TryDequeue(out item))
                    return item;
            }
            if (_selfdestructin54321)
                throw new FileBeingDeletedException();
            throw new Exception("Unable to acquire reader work item.");
        }

        private void ReturnReaderWorkItem(ReaderWorkItem item)
        {
            if (item.IsMemory)
            {
                _memStreams.Enqueue(item);
                if (!_cached || _selfdestructin54321)
                    TryDestructMemStreams();
            }
            else
            {
                _fileStreams.Enqueue(item);
                if (_selfdestructin54321)
                    TryDestructFileStreams();
            }
        }

        public TFChunkBulkReader AcquireReader()
        {
#pragma warning disable 420
            Interlocked.Increment(ref _fileStreamCount);
            if (_selfdestructin54321)
            {
                if (Interlocked.Decrement(ref _fileStreamCount) == 0)
                {
                    // now we should "turn light off"
                    CleanUpFileStreamDestruction();
                }
                throw new FileBeingDeletedException();
            }
#pragma warning restore 420

            // if we get here, then we reserved TFChunk for sure so no one should dispose of chunk file
            // until client returns dedicated reader
            return new TFChunkBulkReader(this, GetSequentialReaderFileStream());
        }

        private Stream GetSequentialReaderFileStream()
        {
            return new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.SequentialScan);
        }

        public void ReleaseReader(TFChunkBulkReader reader)
        {
#pragma warning disable 420
            Interlocked.Decrement(ref _fileStreamCount);
#pragma warning restore 420
            if (_selfdestructin54321 && _fileStreamCount == 0)
                CleanUpFileStreamDestruction();
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
}