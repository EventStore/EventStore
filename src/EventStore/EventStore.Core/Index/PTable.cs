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
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.Settings;
using EventStore.Core.Util;

namespace EventStore.Core.Index
{
    public enum FileType: byte
    {
        PTableFile = 1,
        ChunkFile = 2
    }

    public partial class PTable : ISearchTable, IDisposable
    {
        public const int IndexEntrySize = sizeof(int) + sizeof(int) + sizeof(long);
        public const int MD5Size = 16;
        public const byte Version = 1;
        public const int DefaultBufferSize = 8096;

        private static readonly ILogger Log = LogManager.GetLoggerFor<PTable>();

        public Guid Id { get { return _id; } }
        public int Count { get { return (int)(_size / IndexEntrySize); } }
        public string Filename { get { return _filename; } }

        private readonly Guid _id;
        private readonly int _bufferSize;
        private readonly string _filename;
        private readonly long _size;
        private readonly Midpoint[] _midpoints;
        private readonly Common.Concurrent.ConcurrentQueue<WorkItem> _workItems = new Common.Concurrent.ConcurrentQueue<WorkItem>();

        private readonly ManualResetEventSlim _destroyEvent = new ManualResetEventSlim(false);
        private volatile bool _deleteFile;
        private volatile bool _selfdestructin54321;
        private int _workItemsLeft;

        private PTable(string filename, 
                       Guid id, 
                       int bufferSize = DefaultBufferSize, 
                       int maxReadingThreads = ESConsts.ReadIndexReaderCount, 
                       int depth = 16)
        {
            Ensure.NotNullOrEmpty(filename, "filename");
            Ensure.NotEmptyGuid(id, "id");
            Ensure.Positive(maxReadingThreads, "maxReadingThreads");
            Ensure.Positive(bufferSize, "bufferSize");
            Ensure.Nonnegative(depth, "depth");

            if (!File.Exists(filename)) 
                throw new CorruptIndexException(new PTableNotFoundException(filename));

            _id = id;
            _bufferSize = bufferSize;
            _filename = filename;
            _size = new FileInfo(_filename).Length - PTableHeader.Size - MD5Size;
            File.SetAttributes(_filename, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);

            _workItemsLeft = maxReadingThreads;
            for (int i = 0; i < maxReadingThreads; ++i)
            {
                var workItem = new WorkItem(_filename, IndexEntrySize);
                _workItems.Enqueue(workItem);
            }

            var readerWorkItem = GetWorkItem();
            try
            {
                readerWorkItem.Stream.Seek(0, SeekOrigin.Begin);
                var header = PTableHeader.FromStream(readerWorkItem.Stream);
                if (header.Version != Version)
                    throw new CorruptIndexException(new WrongFileVersionException(_filename, header.Version, Version));
            }
            catch (Exception)
            {
                Dispose();
                throw;
            }
            finally
            {
                ReturnWorkItem(readerWorkItem);
            }

            try
            {
                _midpoints = PopulateCache(depth);
            }
            catch (PossibleToHandleOutOfMemoryException)
            {
                Log.Error("Was unable to create midpoints for PTable. Performance hit possible. OOM Exception.");
            }
        }

        private Midpoint[] PopulateCache(int depth)
        {
            if (depth > 31)
                throw new ArgumentOutOfRangeException("depth");
            if (Count == 0)
                throw new InvalidOperationException("Empty PTable.");

            var workItem = GetWorkItem();
            try
            {
                int segmentSize;
                Midpoint[] midpoints;
                try
                {
                    int midPointsCnt = 1 << depth;
                    if (Count < midPointsCnt)
                    {
                        segmentSize = 1; // we cache all items
                        midpoints = new Midpoint[Count];
                    }
                    else
                    {
                        segmentSize = Count / midPointsCnt;
                        midpoints = new Midpoint[1 + (Count + segmentSize - 1) / segmentSize];
                    }
                }
                catch (OutOfMemoryException exc)
                {
                    throw new PossibleToHandleOutOfMemoryException("Failed to allocate memory for Midpoint cache.", exc);
                }

                for (int x = 0, i = 0, xN = Count - 1; x < xN; x += segmentSize, i += 1)
                {
                    var record = ReadEntry(x, workItem);
                    midpoints[i] = new Midpoint(record.Key, x);
                }

                // add the very last item as the last midpoint (possibly it is done twice)
                {
                    var record = ReadEntry(Count - 1, workItem);
                    midpoints[midpoints.Length - 1] = new Midpoint(record.Key, Count - 1);
                }
                return midpoints;
            }
            finally
            {
                ReturnWorkItem(workItem);
            }
        }

        public void VerifyFileHash()
        {
            var workItem = GetWorkItem();
            try
            {
                workItem.Stream.Seek(0, SeekOrigin.Begin);
                var hash = MD5Hash.GetHashFor(workItem.Stream, 0, workItem.Stream.Length - MD5Size);

                var fileHash = new byte[MD5Size];
                workItem.Stream.Read(fileHash, 0, MD5Size);
                
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
                ReturnWorkItem(workItem);
            }
        }

        public IEnumerable<IndexEntry> IterateAllInOrder()
        {
            // TODO AN: integrate this with general mechanism of work items, so in the middle of iteration 
            // TODO AN: we wouldn't delete the file
            using (var workItem = new WorkItem(_filename, _bufferSize))
            {
                workItem.Stream.Seek(PTableHeader.Size, SeekOrigin.Begin);
                for (int i = 0, n = Count; i < n; i++)
                {
                    yield return ReadNextNoSeek(workItem);
                }
            }
        }

        public bool TryGetOneValue(uint stream, int number, out long position)
        {
            IndexEntry entry;
            if (TryGetOneEntry(stream, number, number, out entry))
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

        private bool TryGetOneEntry(uint stream, int startNumber, int endNumber, out IndexEntry entry)
        {
            Ensure.Nonnegative(startNumber, "startNumber");
            Ensure.Nonnegative(endNumber, "endNumber");

            entry = TableIndex.InvalidIndexEntry;

            var startKey = BuildKey(stream, startNumber);
            var endKey = BuildKey(stream, endNumber);

            if (_midpoints != null && (startKey > _midpoints[0].Key || endKey < _midpoints[_midpoints.Length - 1].Key))
                return false;

            var workItem = GetWorkItem();
            try
            {
                var recordRange = LocateRecordRange(endKey);

                int low = recordRange.Item1;
                int high = recordRange.Item2;
                while (low < high)
                {
                    var mid = low + (high - low) / 2;
                    IndexEntry midpoint = ReadEntry(mid, workItem);
                    if (midpoint.Key <= endKey)
                        high = mid;
                    else
                        low = mid + 1;
                }

                var candEntry = ReadEntry(high, workItem);
                Debug.Assert(candEntry.Key <= endKey);
                if (candEntry.Key < startKey)
                    return false;
                entry = candEntry;
                return true;
            }
            finally
            {
                ReturnWorkItem(workItem);
            }
        }

        public IEnumerable<IndexEntry> GetRange(uint stream, int startNumber, int endNumber)
        {
            Ensure.Nonnegative(startNumber, "startNumber");
            Ensure.Nonnegative(endNumber, "endNumber");

            var result = new List<IndexEntry>();
            var startKey = BuildKey(stream, startNumber);
            var endKey = BuildKey(stream, endNumber);

            if (_midpoints != null && (startKey > _midpoints[0].Key || endKey < _midpoints[_midpoints.Length - 1].Key))
                return result;

            var workItem = GetWorkItem();
            try
            {
                var recordRange = LocateRecordRange(endKey);
                int low = recordRange.Item1;
                int high = recordRange.Item2;
                while (low < high)
                {
                    var mid = low + (high - low) / 2;
                    IndexEntry midpoint = ReadEntry(mid, workItem);
                    if (midpoint.Key <= endKey)
                        high = mid;
                    else
                        low = mid + 1;
                }
                
                for (int i=high, n=Count; i<n; ++i)
                {
                    IndexEntry entry = ReadEntry(i, workItem);
                    Debug.Assert(entry.Key <= endKey);
                    if (entry.Key < startKey)
                        return result;
                    result.Add(entry);
                }
                return result;
            }
            finally
            {
                ReturnWorkItem(workItem);
            }
        }

        private static ulong BuildKey(uint stream, int version)
        {
            return ((uint)version) | (((ulong)stream) << 32);
        }

        private Tuple<int, int> LocateRecordRange(ulong stream)
        {
            var midpoints = _midpoints;
            if (midpoints == null) 
                return Tuple.Create(0, Count);
            int lowerMidpoint = LowerMidpointBound(midpoints, stream);
            int upperMidpoint = UpperMidpointBound(midpoints, stream);
            return Tuple.Create(midpoints[lowerMidpoint].ItemIndex, midpoints[upperMidpoint].ItemIndex);
        }

        private int LowerMidpointBound(Midpoint[] midpoints, ulong stream)
        {
            int l = 0;
            int r = midpoints.Length - 1;
            while (l < r)
            {
                int m = l + (r - l + 1) / 2;
                if (midpoints[m].Key > stream)
                    l = m;
                else
                    r = m - 1;
            }
            return l;
        }

        private int UpperMidpointBound(Midpoint[] midpoints, ulong stream)
        {
            int l = 0;
            int r = midpoints.Length - 1;
            while (l < r)
            {
                int m = l + (r - l) / 2;
                if (midpoints[m].Key < stream)
                    r = m;
                else
                    l = m + 1;
            }
            return r;
        }

        private static IndexEntry ReadEntry(int indexNum, WorkItem workItem)
        {
            workItem.Stream.Seek(IndexEntrySize*(long)indexNum + PTableHeader.Size, SeekOrigin.Begin);
            return ReadNextNoSeek(workItem);
        }

        private static IndexEntry ReadNextNoSeek(WorkItem workItem)
        {
            //workItem.Stream.Read(workItem.Buffer, 0, IndexEntrySize);
            //var entry = (IndexEntry)Marshal.PtrToStructure(workItem.BufferHandle.AddrOfPinnedObject(), typeof(IndexEntry));
            //return entry;
            return new IndexEntry(workItem.Reader.ReadUInt64(), workItem.Reader.ReadInt64());
        }

        private WorkItem GetWorkItem()
        {
            for (int i = 0; i < 10; i++)
            {
                WorkItem item;
                if (_workItems.TryDequeue(out item))
                    return item;
            }
            if (_selfdestructin54321)
                throw new FileBeingDeletedException();
            throw new Exception("Unable to acquire work item.");
        }

        private void ReturnWorkItem(WorkItem workItem)
        {
            _workItems.Enqueue(workItem);
            if (_selfdestructin54321)
                TryDestruct();
        }

        public void MarkForDestruction()
        {
            _deleteFile = true;
            _selfdestructin54321 = true;
            TryDestruct();
        }

        public void Dispose()
        {
            _deleteFile = false;
            _selfdestructin54321 = true;
            TryDestruct();
        }

        private void TryDestruct()
        {
            int workItemCount = int.MaxValue;

            WorkItem workItem;
            while (_workItems.TryDequeue(out workItem))
            {
                workItem.Dispose();
                workItemCount = Interlocked.Decrement(ref _workItemsLeft);
            }

            Debug.Assert(workItemCount >= 0, "Somehow we managed to decrease count of work items below zero.");
            if (workItemCount == 0) // we are the last who should "turn the light off" for file streams
            {
                File.SetAttributes(_filename, FileAttributes.Normal);
                if (_deleteFile)
                    File.Delete(_filename);
                _destroyEvent.Set();
            }
        }

        public void WaitForDestroy(int timeout)
        {
            if (!_destroyEvent.Wait(timeout))
                throw new TimeoutException();
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

        private class WorkItem: IDisposable
        {
            public readonly FileStream Stream;
            //public readonly byte[] Buffer;
            //public readonly GCHandle BufferHandle;
            public readonly BinaryReader Reader;

            public WorkItem(string filename, int bufferSize)
            {
                Stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.RandomAccess);
//                Buffer = new byte[IndexEntrySize];
//                BufferHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
                Reader = new BinaryReader(Stream);
            }

            ~WorkItem()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Stream.Dispose();
                    Reader.Dispose();
//                    if (BufferHandle.IsAllocated)
//                        BufferHandle.Free();
                }
            }
        }
    }
}
