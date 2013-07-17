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

#pragma warning disable 420

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
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Util;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk
{
    public unsafe partial class TFChunk : IDisposable
    {
        public const byte CurrentChunkVersion = 2;
        public const int WriteBufferSize = 8192;
        public const int ReadBufferSize = 8192;

        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunk>();

        public bool IsReadOnly { get { return _isReadOnly; } }
        public bool IsCached { get { return _isCached != 0; } }
        
        // the logical size of data (could be > PhysicalDataSize if scavenged chunk)
        public long LogicalDataSize { get { return Interlocked.Read(ref _logicalDataSize); } }
        // the physical size of data size of data
        public int PhysicalDataSize { get { return _physicalDataSize; } }  

        public string FileName { get { return _filename; } }
        public int FileSize { get { return (int) new FileInfo(_filename).Length; } }

        public ChunkHeader ChunkHeader { get { return _chunkHeader; } }
        public ChunkFooter ChunkFooter { get { return _chunkFooter; } }
        public readonly int MidpointsDepth;

        public int RawWriterPosition 
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
        
        private readonly int _maxReaderCount;
        private readonly Common.Concurrent.ConcurrentQueue<ReaderWorkItem> _fileStreams = new Common.Concurrent.ConcurrentQueue<ReaderWorkItem>();
        private readonly Common.Concurrent.ConcurrentQueue<ReaderWorkItem> _memStreams = new Common.Concurrent.ConcurrentQueue<ReaderWorkItem>();
        private int _internalStreamsCount;
        private int _fileStreamCount;
        private int _memStreamCount;

        private WriterWorkItem _writerWorkItem;
        private long _logicalDataSize;
        private volatile int _physicalDataSize;

        private volatile IntPtr _cachedData;
        private int _cachedLength;
        private volatile int _isCached;

        private readonly ManualResetEventSlim _destroyEvent = new ManualResetEventSlim(false);
        private volatile bool _selfdestructin54321;
        private volatile bool _deleteFile;

        private IChunkReadSide _readSide;

        private TFChunk(string filename, int initialReaderCount, int maxReaderCount, int midpointsDepth)
        {
            Ensure.NotNullOrEmpty(filename, "filename");
            Ensure.Positive(initialReaderCount, "initialReaderCount");
            Ensure.Positive(maxReaderCount, "maxReaderCount");
            if (initialReaderCount > maxReaderCount)
                throw new ArgumentOutOfRangeException("initialReaderCount", "initialReaderCount is greater than maxReaderCount.");
            Ensure.Nonnegative(midpointsDepth, "midpointsDepth");

            _filename = filename;
            _internalStreamsCount = initialReaderCount;
            _maxReaderCount = maxReaderCount;
            MidpointsDepth = midpointsDepth;
        }

        ~TFChunk()
        {
            FreeCachedData();
        }

        public static TFChunk FromCompletedFile(string filename, bool verifyHash)
        {
            var chunk = new TFChunk(filename, ESConsts.TFChunkInitialReaderCount, ESConsts.TFChunkMaxReaderCount, TFConsts.MidpointsDepth);
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
            var chunk = new TFChunk(filename, ESConsts.TFChunkInitialReaderCount, ESConsts.TFChunkMaxReaderCount, TFConsts.MidpointsDepth);
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
            var chunkHeader = new ChunkHeader(CurrentChunkVersion, chunkSize, chunkStartNumber, chunkEndNumber, isScavenged, Guid.NewGuid());
            return CreateWithHeader(filename, chunkHeader, chunkSize + ChunkHeader.Size + ChunkFooter.Size);
        }

        public static TFChunk CreateWithHeader(string filename, ChunkHeader header, int fileSize)
        {
            var chunk = new TFChunk(filename, ESConsts.TFChunkInitialReaderCount, ESConsts.TFChunkMaxReaderCount, TFConsts.MidpointsDepth);
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
            SetAttributes();
            CreateReaderStreams();

            var reader = GetReaderWorkItem();
            try
            {
                _chunkHeader = ReadHeader(reader.Stream);
                if (_chunkHeader.Version != CurrentChunkVersion)
                    throw new CorruptDatabaseException(new WrongFileVersionException(_filename, _chunkHeader.Version, CurrentChunkVersion));

                _chunkFooter = ReadFooter(reader.Stream);
                if (!_chunkFooter.IsCompleted)
                {
                    throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                        string.Format("Chunk file '{0}' should be completed, but is not.", _filename)));
                }

                _logicalDataSize = _chunkFooter.LogicalDataSize;
                _physicalDataSize = _chunkFooter.PhysicalDataSize;

                var expectedFileSize = _chunkFooter.PhysicalDataSize + _chunkFooter.MapSize + ChunkHeader.Size + ChunkFooter.Size;
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

            _readSide = _chunkHeader.IsScavenged ? (IChunkReadSide) new TFChunkReadSideScavenged(this) : new TFChunkReadSideUnscavenged(this);
            _readSide.Cache();

            if (verifyHash)
                VerifyFileHash();
        }

        private void InitNew(ChunkHeader chunkHeader, int fileSize)
        {
            Ensure.NotNull(chunkHeader, "chunkHeader");
            Ensure.Positive(fileSize, "fileSize");

            _isReadOnly = false;
            _chunkHeader = chunkHeader;
            _physicalDataSize = 0;
            _logicalDataSize = 0;

            SetAttributes();
            CreateWriterWorkItemForNewChunk(chunkHeader, fileSize);
            CreateReaderStreams();

            _readSide = chunkHeader.IsScavenged ? (IChunkReadSide) new TFChunkReadSideScavenged(this) : new TFChunkReadSideUnscavenged(this);

        }

        private void InitOngoing(int writePosition, bool checkSize)
        {
            Ensure.Nonnegative(writePosition, "writePosition");
            if (!File.Exists(_filename))
                throw new CorruptDatabaseException(new ChunkNotFoundException(_filename));

            _isReadOnly = false;
            _physicalDataSize = writePosition;
            _logicalDataSize = writePosition;

            SetAttributes();
            CreateWriterWorkItemForExistingChunk(writePosition, out _chunkHeader);
            if (_chunkHeader.Version != CurrentChunkVersion)
                throw new CorruptDatabaseException(new WrongFileVersionException(_filename, _chunkHeader.Version, CurrentChunkVersion));
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
        }

        private void CreateReaderStreams()
        {
            Interlocked.Add(ref _fileStreamCount, _internalStreamsCount);
            for (int i = 0; i < _internalStreamsCount; i++)
            {
                _fileStreams.Enqueue(CreateInternalReaderWorkItem());
            }
        }

        private ReaderWorkItem CreateInternalReaderWorkItem()
        {
            var stream = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                                        ReadBufferSize, FileOptions.RandomAccess);
            var reader = new BinaryReader(stream);
            return new ReaderWorkItem(stream, reader, false);
        }

        private void CreateWriterWorkItemForNewChunk(ChunkHeader chunkHeader, int fileSize)
        {
            var md5 = MD5.Create();

            // create temp file first and set desired length
            // if there is not enough disk space or something else prevents file to be resized as desired
            // we'll end up with empty temp file, which won't trigger false error on next DB verification
            var tempFilename = _filename + ".tmp";
            var tempFile = new FileStream(tempFilename, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read,
                                          WriteBufferSize, FileOptions.SequentialScan);
            tempFile.SetLength(fileSize);

            // we need to write header into temp file before moving it into correct chunk place, so in case of crash
            // we don't end up with seemingly valid chunk file with no header at all...
            WriteHeader(md5, tempFile, chunkHeader);

            tempFile.Flush(flushToDisk: true);
            tempFile.Close();
            File.Move(tempFilename, _filename);

            var stream = new FileStream(_filename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read,
                                        WriteBufferSize, FileOptions.SequentialScan);
            stream.Position = ChunkHeader.Size;
            var writer = new BinaryWriter(stream);
            _writerWorkItem = new WriterWorkItem(stream, writer, md5);
            Flush(); // persist file move result
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
            var realPosition = GetRawPosition(writePosition);
            MD5Hash.ContinuousHashFor(md5, stream, 0, realPosition);
            stream.Position = realPosition; // this reordering fixes bug in Mono implementation of FileStream
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
            // in mono SetAttributes on non-existing file throws exception, in windows it just works silently.
            Helper.EatException(() =>
            {
                if (_isReadOnly)
                    File.SetAttributes(_filename, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);
                else
                    File.SetAttributes(_filename, FileAttributes.NotContentIndexed);
            });
        }

        public void VerifyFileHash()
        {
            if (!IsReadOnly)
                throw new InvalidOperationException("You can't verify hash of not-completed TFChunk.");

            Log.Trace("Verifying hash for TFChunk '{0}'...", _filename);

            using (var reader = AcquireReader())
            {
                var footer = _chunkFooter;

                byte[] hash;
                using (var md5 = MD5.Create())
                {
                    reader.Stream.Seek(0, SeekOrigin.Begin);
                    // hash header and data
                    MD5Hash.ContinuousHashFor(md5, reader.Stream, 0, ChunkHeader.Size + footer.PhysicalDataSize);
                    // hash mapping and footer except MD5 hash sum which should always be last
                    MD5Hash.ContinuousHashFor(md5, 
                                              reader.Stream,
                                              ChunkHeader.Size + footer.PhysicalDataSize,
                                              footer.MapSize + ChunkFooter.Size - ChunkFooter.ChecksumSize);
                    md5.TransformFinalBlock(Empty.ByteArray, 0, 0);
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

        private static long GetRawPosition(long logicalPosition)
        {
            return ChunkHeader.Size + logicalPosition;
        }

        private static long GetDataPosition(WriterWorkItem workItem)
        {
            return workItem.Stream.Position - ChunkHeader.Size;
        }

        // WARNING CacheInMemory/UncacheFromMemory should not be called simultaneously !!!
        public void CacheInMemory()
        {
            if (Interlocked.CompareExchange(ref _isCached, 1, 0) == 0)
            {
                // we won the right to cache
                var sw = Stopwatch.StartNew();

                try
                {
                    BuildCacheArray();
                }
                catch (OutOfMemoryException)
                {
                    Log.Error("CACHING FAILED due to OutOfMemory exception in TFChunk #{0}-{1} at {2}.",
                              _chunkHeader.ChunkStartNumber, _chunkHeader.ChunkEndNumber, Path.GetFileName(_filename));
                    _isCached = 0;
                    return;
                }
                catch (FileBeingDeletedException)
                {
                    Log.Debug("CACHING FAILED due to FileBeingDeleted exception (TFChunk is being disposed) in TFChunk #{0}-{1} at {2}.",
                              _chunkHeader.ChunkStartNumber, _chunkHeader.ChunkEndNumber, Path.GetFileName(_filename));
                    _isCached = 0;
                    return;
                }

                BuildCacheReaders();
                _readSide.Uncache();

                var writerWorkItem = _writerWorkItem;
                if (writerWorkItem != null)
                {
                    writerWorkItem.UnmanagedMemoryStream =
                        new UnmanagedMemoryStream((byte*) _cachedData, _cachedLength, _cachedLength, FileAccess.ReadWrite);
                }

                Log.Trace("CACHED TFChunk #{0}-{1} ({2}) in {3}.", 
                          _chunkHeader.ChunkStartNumber, _chunkHeader.ChunkEndNumber, Path.GetFileName(_filename), sw.Elapsed);
            }
        }

        private void BuildCacheArray()
        {
            var workItem = GetReaderWorkItem();
            try
            {
                if (workItem.IsMemory)
                    throw new InvalidOperationException("When trying to build cache, reader worker is already in-memory reader.");

                var dataSize = _isReadOnly ? _physicalDataSize + ChunkFooter.MapSize : _chunkHeader.ChunkSize;
                _cachedLength = ChunkHeader.Size + dataSize + ChunkFooter.Size;
                var cachedData = Marshal.AllocHGlobal(_cachedLength);
                try
                {
                    using (var unmanagedStream = new UnmanagedMemoryStream((byte*)cachedData, _cachedLength, _cachedLength, FileAccess.ReadWrite))
                    {
                        workItem.Stream.Seek(0, SeekOrigin.Begin);
                        var buffer = new byte[65536];
                        // in ongoing chunk there is no need to read everything, it's enough to read just actual data written
                        int toRead = _isReadOnly ? _cachedLength : ChunkHeader.Size + _physicalDataSize; 
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
            Interlocked.Add(ref _memStreamCount, _maxReaderCount);
            for (int i = 0; i < _maxReaderCount; i++)
            {
                var stream = new UnmanagedMemoryStream((byte*)_cachedData, _cachedLength);
                var reader = new BinaryReader(stream);
                _memStreams.Enqueue(new ReaderWorkItem(stream, reader, isMemory: true));
            }
        }

        //WARNING CacheInMemory/UncacheFromMemory should not be called simultaneously !!!
        public void UnCacheFromMemory()
        {
            if (Interlocked.CompareExchange(ref _isCached, 0, 1) == 1)
            {
                // we won the right to un-cache and chunk was cached
                // NOTE: calling simultaneously cache and uncache is very dangerous
                // NOTE: though multiple simultaneous calls to either Cache or Uncache is ok

                _readSide.Cache();

                var writerWorkItem = _writerWorkItem;
                var unmanagedMemStream = writerWorkItem == null ? null : writerWorkItem.UnmanagedMemoryStream;
                if (unmanagedMemStream != null)
                {
                    unmanagedMemStream.Dispose();
                    writerWorkItem.UnmanagedMemoryStream = null;
                }

                TryDestructMemStreams();

                Log.Trace("UNCACHED TFChunk #{0}-{1} ({2})", _chunkHeader.ChunkStartNumber, _chunkHeader.ChunkEndNumber, Path.GetFileName(_filename));
            }
        }

        public RecordReadResult TryReadAt(long logicalPosition)
        {
            return _readSide.TryReadAt(logicalPosition);
        }

        public RecordReadResult TryReadFirst()
        {
            return _readSide.TryReadFirst();
        }

        public RecordReadResult TryReadClosestForward(long logicalPosition)
        {
            return _readSide.TryReadClosestForward(logicalPosition);
        }

        public RecordReadResult TryReadLast()
        {
            return _readSide.TryReadLast();
        }

        public RecordReadResult TryReadClosestBackward(long logicalPosition)
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
                return RecordWriteResult.Failed(GetDataPosition(workItem));

            var oldPosition = WriteRawData(workItem, buffer);
            _physicalDataSize = (int)GetDataPosition(workItem); // should fit 32 bits
            _logicalDataSize = ChunkHeader.GetLocalLogPosition(record.LogPosition + length + 2*sizeof(int));
            
            // for non-scavenged chunk _physicalDataSize should be the same as _logicalDataSize
            // for scavenged chunk _logicalDataSize should be at least the same as _physicalDataSize
            if ((!ChunkHeader.IsScavenged && _logicalDataSize != _physicalDataSize)
                || (ChunkHeader.IsScavenged && _logicalDataSize < _physicalDataSize))
            {
                throw new Exception(string.Format("Data sizes violation. Chunk: {0}, IsScavenged: {1}, LogicalDataSize: {2}, PhysicalDataSize: {3}.",
                                                  FileName, ChunkHeader.IsScavenged, _logicalDataSize, _physicalDataSize));
            }
            
            return RecordWriteResult.Successful(oldPosition, _physicalDataSize);
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
            var curPos = GetDataPosition(workItem);

            //MD5
            workItem.MD5.TransformBlock(buf, 0, len, null, 0);
            //FILE
            workItem.Stream.Write(buf, 0, len); // as we are always append-only, stream's position should be right here
            //MEMORY
            var memStream = workItem.UnmanagedMemoryStream;
            if (memStream != null)
            {
                var realMemPos = GetRawPosition(curPos);
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
            if (ChunkHeader.IsScavenged)
                throw new InvalidOperationException("CompleteScavenged should be used for scavenged chunks!");
            CompleteNonRaw(null);
        }

        public void CompleteScavenge(ICollection<PosMap> mapping)
        {
            if (!ChunkHeader.IsScavenged)
                throw new InvalidOperationException("CompleteScavenged should not be used for non-scavenged chunks!");

            CompleteNonRaw(mapping);
        }

        private void CompleteNonRaw(ICollection<PosMap> mapping)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("Cannot complete a read-only TFChunk.");

            _chunkFooter = WriteFooter(mapping);
            Flush();
            _isReadOnly = true;

            CleanUpWriterWorkItem(_writerWorkItem);
            _writerWorkItem = null;
            SetAttributes();
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
            SetAttributes();
        }

        private ChunkFooter WriteFooter(ICollection<PosMap> mapping)
        {
            var workItem = _writerWorkItem;

            int mapSize = 0;
            if (mapping != null)
            {
                if (_isCached != 0)
                {
                    throw new InvalidOperationException("Trying to write mapping while chunk is cached! "
                                                      + "You probably are writing scavenged chunk as cached. "
                                                      + "Don't do this!");
                }
                mapSize = mapping.Count * PosMap.FullSize;
                workItem.Buffer.SetLength(mapSize);
                workItem.Buffer.Position = 0;
                foreach (var map in mapping)
                {
                    map.Write(workItem.BufferWriter);
                }
                WriteRawData(workItem, workItem.Buffer);
            }

            var footerNoHash = new ChunkFooter(true, true, _physicalDataSize, LogicalDataSize, mapSize, new byte[ChunkFooter.ChecksumSize]);
            //MD5
            workItem.MD5.TransformFinalBlock(footerNoHash.AsByteArray(), 0, ChunkFooter.Size - ChunkFooter.ChecksumSize);
            //FILE
            var footerWithHash = new ChunkFooter(true, true, _physicalDataSize, LogicalDataSize, mapSize, workItem.MD5.Hash);
            workItem.Stream.Write(footerWithHash.AsByteArray(), 0, ChunkFooter.Size);

            Flush(); // trying to prevent bug with resized file, but no data in it

            var fileSize = ChunkHeader.Size + _physicalDataSize + mapSize + ChunkFooter.Size;
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
                fileStreamCount = Interlocked.Decrement(ref _fileStreamCount);
            }

            if (fileStreamCount < 0)
                throw new Exception("Somehow we managed to decrease count of file streams below zero.");
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
            if (memStreamCount < 0)
                throw new Exception("Somehow we managed to decrease count of memory streams below zero.");
            if (memStreamCount == 0) // we are the last who should "turn the light off" for memory streams
                FreeCachedData();
        }

        private void FreeCachedData()
        {
            var cachedData = Interlocked.Exchange(ref _cachedData, IntPtr.Zero);
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
            if (_selfdestructin54321)
                throw new FileBeingDeletedException();

            ReaderWorkItem item;
            // try get memory stream reader first
            if (_memStreams.TryDequeue(out item))
                return item;
            if (_fileStreams.TryDequeue(out item))
                return item;

            if (_selfdestructin54321)
                throw new FileBeingDeletedException();

            var internalStreamCount = Interlocked.Increment(ref _internalStreamsCount);
            if (internalStreamCount > _maxReaderCount)
                throw new Exception("Unable to acquire reader work item. Max internal streams limit reached.");

            Interlocked.Increment(ref _fileStreamCount);
            if (_selfdestructin54321)
            {
                if (Interlocked.Decrement(ref _fileStreamCount) == 0)
                    CleanUpFileStreamDestruction(); // now we should "turn light off"
                throw new FileBeingDeletedException();
            }

            // if we get here, then we reserved TFChunk for sure so no one should dispose of chunk file
            // until client returns the reader
            return CreateInternalReaderWorkItem();
        }

        private void ReturnReaderWorkItem(ReaderWorkItem item)
        {
            if (item.IsMemory)
            {
                _memStreams.Enqueue(item);
                if (_isCached == 0 || _selfdestructin54321)
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
            var fileStreamCount = Interlocked.Decrement(ref _fileStreamCount);
            if (fileStreamCount < 0)
                throw new Exception("Somehow we managed to decrease count of file streams below zero.");
            if (_selfdestructin54321 && fileStreamCount == 0)
                CleanUpFileStreamDestruction();
        }

        private struct Midpoint
        {
            public readonly int ItemIndex;
            public readonly long LogPos;

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