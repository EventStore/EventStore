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
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Exceptions;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;

namespace EventStore.Core.Index
{
    public enum FileType
    {
        PTableFile = 1,
        ChunkFile = 2
    }

    public unsafe class PTable : ISearchTable, IDisposable
    {
        public const int MD5Size = 16;
        public const int Version = 1;

        private static readonly ILogger Log = LogManager.GetLoggerFor<PTable>();

        public Guid Id { get { return _id; } }
        public int Count { get { return (int)(_size >> 4); } }
        public string Filename { get { return _filename; } }

        private readonly string _filename;
        private readonly int _bufferSize;
        private readonly int _maxReadingThreads;
        private readonly long _size;
        private readonly GCHandle _bufferPtr;
        private readonly byte[] _buffer;
        private readonly Common.Concurrent.ConcurrentQueue<FileStream> _streams = new Common.Concurrent.ConcurrentQueue<FileStream>();

        private readonly Midpoint[] _midpoints;
        private readonly ManualResetEventSlim _destroyEvent = new ManualResetEventSlim(false);
        private readonly Guid _id;

        private volatile bool _selfdestructin54321;

        private PTable(string filename, 
                       Guid id, 
                       int bufferSize = 8096, 
                       int maxReadingThreads = ESConsts.ReadIndexReaderCount, 
                       int depth = 16)
        {
            if (!File.Exists(filename)) 
                throw new CorruptIndexException(new PTableNotFoundException(filename));

            _id = id;
            _size = new FileInfo(filename).Length - PTableHeader.Size - MD5Size;

            _filename = filename;
            File.SetAttributes(_filename, FileAttributes.ReadOnly);
            File.SetAttributes(_filename, FileAttributes.NotContentIndexed);

            _bufferSize = bufferSize;
            _maxReadingThreads = maxReadingThreads;
            _buffer = new byte[16];
            _bufferPtr = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

            using (var stream = File.OpenRead(filename))
            using (var reader = new BinaryReader(stream))
            {
                PTableHeader.FromStream(reader);
            }
            
            for (int i = 0; i < _maxReadingThreads;i++ )
            {
                var s = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read, 16, FileOptions.RandomAccess);
                _streams.Enqueue(s);
            }

            try
            {
                _midpoints = PopulateCache(depth);
            }
            catch (PossibleToHandleOutOfMemoryException)
            {
                Log.Info("Was unable to create midpoints for ptable. Performance hit possible OOM Exception.");
            }
        }

        public void VerifyFileHash()
        {
            var stream = GetFileStream();
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                var hash = MD5Hash.GetHashFor(stream, 0, stream.Length - MD5Size);

                stream.Seek(-MD5Size, SeekOrigin.End);
                var fileHash = new byte[MD5Size];
                stream.Read(fileHash, 0, MD5Size);
                
                if (hash == null || fileHash.Length != hash.Length) 
                    throw new CorruptIndexException(new HashValidationException());

                for (int i = 0; i < fileHash.Length; i++)
                {
                    if (fileHash[i] != hash[i])
                        throw new CorruptIndexException(new HashValidationException());
                }
            }
            finally
            {
                ReturnStream(stream);
            }
        }

        public void MarkForDestruction()
        {
            _selfdestructin54321 = true;
            TryDestruct();
        }

        private Midpoint[] PopulateCache(int depth)
        {
            if (Count == 0)
                throw new InvalidOperationException("Empty PTable.");

            var buffer = new byte[16];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            FileStream stream = null;
            try
            {
                stream = GetFileStream();
                int midPointsCnt = 1 << depth;
                int segmentSize;
                Midpoint[] midpoints;
                if (Count < midPointsCnt)
                {
                    segmentSize = 1; // we cache all items
                    try
                    {
                        midpoints = new Midpoint[Count];
                    }
                    catch (OutOfMemoryException)
                    {
                        throw new PossibleToHandleOutOfMemoryException();
                    }
                }
                else
                {
                    segmentSize = Count / midPointsCnt;
                    try
                    {
                        midpoints = new Midpoint[1 + (Count + segmentSize - 1)/segmentSize];
                    }
                    catch(OutOfMemoryException)
                    {
                        throw new PossibleToHandleOutOfMemoryException();
                    }
                }

                for (int x = 0, i = 0, xN = Count - 1; x < xN; x += segmentSize, i += 1)
                {
                    stream.Seek(PTableHeader.Size + (((long)x) << 4), SeekOrigin.Begin);
                    var record = ReadNext(stream, buffer, handle);
                    midpoints[i] = new Midpoint(record.Key, x);
                }

                // add the very last item as the last midpoint (possibly it is done twice)
                {
                    stream.Seek(PTableHeader.Size + (((long)(Count - 1)) << 4), SeekOrigin.Begin);
                    var record = ReadNext(stream, buffer, handle);
                    midpoints[midpoints.Length - 1] = new Midpoint(record.Key, Count - 1);
                }
                return midpoints;
            }
            finally
            {
                if (stream != null)
                    ReturnStream(stream);
                handle.Free();
            }
        }

        ~PTable()
        {
            if (_bufferPtr.IsAllocated)
                _bufferPtr.Free();
        }

        public static PTable FromFile(string filename)
        {
            return new PTable(filename, Guid.NewGuid());
        }

        public static PTable FromMemtable(IMemTable table, string filename, int cacheDepth = 16)
        {
            if (table == null) throw new ArgumentNullException("table");
            if (filename == null) throw new ArgumentNullException("filename");

            Log.Trace("Started dumping MemTable [{0}] into PTable...", table.Id);
            using (var f = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 8096, 
                                          FileOptions.SequentialScan))
            {
                f.SetLength(PTableHeader.Size + (table.Count << 4) + MD5Size); // EXACT SIZE
                f.Seek(0, SeekOrigin.Begin);

                var md5 = MD5.Create();
                var buffer = new byte[16];
                using (var cs = new CryptoStream(f, md5, CryptoStreamMode.Write))
                using (var b = new BufferedStream(cs, 65536))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    // WRITE INDEX ENTRIES
                    foreach (var record in table.IterateAllInOrder())
                    {
                        var x = record;
                        AppendRecordTo(b, x.Bytes, buffer);
                    }
                    b.Flush();
                    cs.FlushFinalBlock();

                    // WRITE MD5
                    var hash = md5.Hash;
                    f.Write(hash, 0, hash.Length);
                }
                f.Close();
                Log.Trace("Done dumping MemTable [{0}].", table.Id);
            }
            return new PTable(filename, table.Id, depth: cacheDepth);
        }

        public static PTable MergeTo(ICollection<PTable> tables, 
                                     string outputFile, 
                                     Func<IndexEntry, bool> isHashCollision,
                                     int cacheDepth = 16)
        {
            var enumerators = tables.Select(table => table.IterateAllInOrder().GetEnumerator()).ToList();
            var fileSize = GetFileSize(tables); // approximate file size
            if (enumerators.Count == 2)
                return MergeTo2(enumerators, fileSize, outputFile, isHashCollision, cacheDepth);
            Log.Trace("PTables merge started.");
            var watch = new Stopwatch();
            watch.Start();
            for (int i=0;i<enumerators.Count;i++)
            {
                if (!enumerators[i].MoveNext())
                {
                    enumerators[i].Dispose();
                    enumerators.RemoveAt(i);
                    i--;
                }
            }
            var bytes = new byte[16];
            uint lastdeleted = uint.MaxValue;
            var md5 = MD5.Create();
            using (var f = new FileStream(outputFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, 
                                          FileOptions.SequentialScan))
            {
                f.SetLength(fileSize);
                f.Seek(0, SeekOrigin.Begin);

                using (var cs = new CryptoStream(f, md5, CryptoStreamMode.Write))
                using (var b = new BufferedStream(cs, 65536))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    // WRITE INDEX ENTRIES
                    while (enumerators.Count > 0)
                    {
                        var idx = GetMaxOf(enumerators);
                        var current = enumerators[idx].Current;
                        if (current.Version == int.MaxValue && !isHashCollision(current))
                        {
                            lastdeleted = current.Stream;
                            AppendRecordTo(b, current.Bytes, bytes);
                        }
                        else
                        {
                            if (lastdeleted != current.Stream)
                                AppendRecordTo(b, current.Bytes, bytes);
                        }
                        if (!enumerators[idx].MoveNext())
                        {
                            enumerators[idx].Dispose();
                            enumerators.RemoveAt(idx);
                        }
                    }
                    f.SetLength(f.Position + MD5Size);
                    b.Flush();
                    cs.FlushFinalBlock();
                        
                    // WRITE MD5
                    var hash = md5.Hash;
                    f.Write(hash, 0, hash.Length);
                }
            }
            Log.Trace("PTables merge finished in " + watch.Elapsed);
            return new PTable(outputFile, Guid.NewGuid(), depth: cacheDepth);
        }

        private static PTable MergeTo2(List<IEnumerator<IndexEntry>> enumerators, 
                                       long fileSize, 
                                       string outputFile, 
                                       Func<IndexEntry, bool> isHashCollision,
                                       int cacheDepth)
        {
            Log.Trace("PTables merge started (specialized for <= 2 tables).");
            var watch = Stopwatch.StartNew();
            var bytes = new byte[16];
            uint lastdeleted = uint.MaxValue;
            var md5 = MD5.Create();
            using (var f = new FileStream(outputFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1000000, FileOptions.SequentialScan))
            {
                f.SetLength(fileSize);
                f.Seek(0, SeekOrigin.Begin);

                using (var cs = new CryptoStream(f, md5, CryptoStreamMode.Write))
                using (var b = new BufferedStream(cs, 65536))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    // WRITE INDEX ENTRIES
                    var enum1 = enumerators[0];
                    var enum2 = enumerators[1];
                    bool available1 = enum1.MoveNext();
                    bool available2 = enum2.MoveNext();
                    IndexEntry current;
                    while (available1 || available2)
                    {
                        if (available1 && (!available2 || enum1.Current.CompareTo(enum2.Current) > 0))
                        {
                            current = enum1.Current;
                            available1 = enum1.MoveNext();
                        }
                        else 
                        {
                            current = enum2.Current;
                            available2 = enum2.MoveNext();
                        }

                        if (current.Version == int.MaxValue && !isHashCollision(current))
                        {
                            lastdeleted = current.Stream;
                            AppendRecordTo(b, current.Bytes, bytes);
                        }
                        else
                        {
                            if (lastdeleted != current.Stream)
                                AppendRecordTo(b, current.Bytes, bytes);
                        }
                    }
                    f.SetLength(f.Position + MD5Size);
                    b.Flush();
                    cs.FlushFinalBlock();

                    // WRITE MD5
                    var hash = md5.Hash;
                    f.Write(hash, 0, hash.Length);
                }
            }
            Log.Trace("PTables merge finished in " + watch.Elapsed);
            return new PTable(outputFile, Guid.NewGuid(), depth: cacheDepth);
        }

        private static long GetFileSize(IEnumerable<PTable> tables)
        {
            long ret = 0;
            foreach(var table in tables)
            {
                ret += table.Count;
            }
            return PTableHeader.Size + (ret << 4) + MD5Size; // plus md5
        }

        private static void AppendRecordTo(Stream stream, byte* bytes, byte[] buffer)
        {
            Marshal.Copy(new IntPtr(bytes), buffer, 0, 16);
            stream.Write(buffer, 0, 16);     
        }

        private static int GetMaxOf(List<IEnumerator<IndexEntry>> enumerators)
        {
            //TODO GFY IF WE LIMIT THIS TO FOUR WE CAN UNROLL THIS LOOP AND WILL BE FASTER
            var max = new IndexEntry(ulong.MinValue, long.MinValue);
            int idx = 0;
            for (int i=0;i<enumerators.Count;i++)
            {
                var cur = enumerators[i].Current;
                if (cur.CompareTo(max) > 0)
                {
                    max = cur;
                    idx = i;
                }
            }
            return idx;
        }

        private static ulong BuildKey(uint stream, int version)
        {
            return ((uint)version) | (((ulong)stream) << 32);
        }

        public bool TryGetOneValue(uint stream, int version, out long position)
        {
            IndexEntry entry;
            if (TryGetOneEntry(stream, version, version, out entry))
            {
                position = entry.Position;
                return true;
            }
            position = -1;
            return false;
        }

        public bool TryGetLatestEntry(uint stream, out IndexEntry entry)
        {
            return TryGetOneEntry(stream, 0, int.MaxValue, out entry);
        }

        private bool TryGetOneEntry(uint stream, int startVersion, int endVersion, out IndexEntry entry)
        {
            if (startVersion < 0)
                throw new ArgumentOutOfRangeException("startVersion");
            if (endVersion < 0)
                throw new ArgumentOutOfRangeException("endVersion");

            if (_selfdestructin54321)
                throw new FileBeingDeletedException();

            entry = TableIndex.InvalidIndexEntry;

            var startKey = BuildKey(stream, startVersion);
            var endKey = BuildKey(stream, endVersion);

            if (_midpoints != null && (startKey > _midpoints[0].Key || endKey < _midpoints[_midpoints.Length - 1].Key))
                return false;

            var readbuffer = new byte[16];
            var handle = GCHandle.Alloc(readbuffer, GCHandleType.Pinned);
            FileStream file = null;
            try
            {
                file = GetFileStream();

                var recordRange = LocateRecordRange(endKey);
                int low = recordRange.Item1;
                int high = recordRange.Item2;
                while (low < high)
                {
                    var mid = low + (high - low) / 2;

                    file.Seek(PTableHeader.Size + (((long)mid) << 4), SeekOrigin.Begin);
                    IndexEntry midpoint = ReadNext(file, readbuffer, handle);

                    if (midpoint.Key <= endKey)
                        high = mid;
                    else
                        low = mid + 1;
                }

                file.Seek(PTableHeader.Size + (((long)high) << 4), SeekOrigin.Begin);
                var candEntry = ReadNext(file, readbuffer, handle);

                Debug.Assert(candEntry.Key <= endKey);

                if (candEntry.Key < startKey)
                    return false;
                entry = candEntry;
                return true;
            }
            finally
            {
                if (file != null)
                    ReturnStream(file);
                handle.Free();
            }
        }

        public IEnumerable<IndexEntry> GetRange(uint stream, int startVersion, int endVersion)
        {
            //TODO GFY do we want to make interface List<> so we all know this MUST NOT be returned as yield?
            if (startVersion < 0)
                throw new ArgumentOutOfRangeException("startVersion");
            if (endVersion < 0)
                throw new ArgumentOutOfRangeException("endVersion");

            if (_selfdestructin54321)
                throw new FileBeingDeletedException();

            var ret = new List<IndexEntry>();
            var startKey = BuildKey(stream, startVersion);
            var endKey = BuildKey(stream, endVersion);

            if (_midpoints != null && (startKey > _midpoints[0].Key || endKey < _midpoints[_midpoints.Length - 1].Key))
                return ret;

            var readbuffer = new byte[16];
            var handle = GCHandle.Alloc(readbuffer, GCHandleType.Pinned);
            FileStream file = null;
            try
            {
                file = GetFileStream();

                var recordRange = LocateRecordRange(endKey);
                int low = recordRange.Item1;
                int high = recordRange.Item2;
                while (low < high)
                {
                    var mid = low + (high - low) / 2;

                    file.Seek(PTableHeader.Size + (((long)mid) << 4), SeekOrigin.Begin);
                    IndexEntry midpoint = ReadNext(file, readbuffer, handle);

                    if (midpoint.Key <= endKey)
                        high = mid;
                    else
                        low = mid + 1;
                }
                
                for (int i=high; i<Count; ++i)
                {
                    file.Seek(PTableHeader.Size + (((long)i) << 4), SeekOrigin.Begin);
                    IndexEntry entry = ReadNext(file, readbuffer, handle);
                    
                    Debug.Assert(entry.Key <= endKey);

                    if (entry.Key < startKey)
                        return ret;
                    ret.Add(entry);
                }
                return ret;
            }
            finally
            {
                if (file != null)
                    ReturnStream(file);
                handle.Free();
            }
        }

        private Tuple<int, int> LocateRecordRange(ulong stream)
        {
            if (_midpoints == null) return Tuple.Create(0, Count);
            int lowerMidpoint = LowerMidpointBound(stream);
            int upperMidpoint = UpperMidpointBound(stream);
            return Tuple.Create(_midpoints[lowerMidpoint].ItemIndex, _midpoints[upperMidpoint].ItemIndex);
        }

        /// <summary>
        /// Returns the index of lower midpoint for given stream.
        /// Assumes it always exist.
        /// </summary>
        private int LowerMidpointBound(ulong stream)
        {
            if (_midpoints == null) return 0;
            int l = 0;
            int r = _midpoints.Length - 1;
            while (l < r)
            {
                int m = l + (r - l + 1) / 2;
                if (_midpoints[m].Key > stream)
                    l = m;
                else
                    r = m - 1;
            }
            return l;
        }

        private int UpperMidpointBound(ulong stream)
        {
            if (_midpoints == null) return Count;
            int l = 0;
            int r = _midpoints.Length - 1;
            while (l < r)
            {
                int m = l + (r - l) / 2;
                if (_midpoints[m].Key < stream)
                    r = m;
                else
                    l = m + 1;
            }
            return r;
        }

        private void ReturnStream(FileStream stream)
        {
            _streams.Enqueue(stream);
            if (_selfdestructin54321)
            {
                TryDestruct();
            }
        }

        public void WaitForDestroy(int timeout)
        {
            if (!_destroyEvent.Wait(timeout))
                throw new TimeoutException();
        }

        private void TryDestruct()
        {
            if (_streams.Count == _maxReadingThreads)
            {
                File.SetAttributes(_filename, FileAttributes.Normal);
                _streams.ToList().ForEach(x => {x.Close(); x.Dispose();});
                File.Delete(_filename);
                _destroyEvent.Set();
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    WaitForDestroy(10000);
                }
                catch (Exception exc)
                {
                    Log.ErrorException(exc, "PTable {0} haven't destroyed in 10 seconds.", Id);
                }
            });
        }

        private FileStream GetFileStream()
        {
            FileStream stream;
            for (int i = 0; i < 10; i++)
            {
                if (_selfdestructin54321)
                    throw new FileBeingDeletedException();
                if (_streams.TryDequeue(out stream))
                {
                    return stream;
                }
            }
            throw new Exception("Unable to acquire stream.");
        }

        public IEnumerable<IndexEntry> IterateAllInOrder()
        {
            //TODO GFY this should use a stream that keeps the lock.
            using (var f = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, FileOptions.SequentialScan))
            {
                f.Seek(PTableHeader.Size, SeekOrigin.Begin);
                for (int i = 0; i < Count; i++)
                {
                     yield return ReadNext(f, _buffer, _bufferPtr);
                }
            }
        }

        private static IndexEntry ReadNext(FileStream fileStream, byte [] buffer, GCHandle handle)
        {
            fileStream.Read(buffer, 0, 16);
            var entry = (IndexEntry)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(IndexEntry));
            return entry;
        }

        public void Dispose()
        {
            foreach (var stream in _streams)
            {
                stream.Dispose();
            }
        }

        private struct Midpoint
        {
            public readonly ulong Key;
            public readonly int ItemIndex;

            public Midpoint(ulong key, int itemIndex)
            {
                Key = key;
                ItemIndex = itemIndex;
            }
        }
    }
}
