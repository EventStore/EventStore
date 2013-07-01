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
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.DataStructures;
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
        public const int DefaultBufferSize = 8192;
        public const int DefaultSequentialBufferSize = 65536;

        private static readonly ILogger Log = LogManager.GetLoggerFor<PTable>();

        public Guid Id { get { return _id; } }
        public int Count { get { return (int)(_size / IndexEntrySize); } }
        public string Filename { get { return _filename; } }

        private readonly Guid _id;
        private readonly int _bufferSize;
        private readonly string _filename;
        private readonly long _size;
        private readonly Midpoint[] _midpoints;
        private readonly ObjectPool<WorkItem> _workItems;

        private readonly ManualResetEventSlim _destroyEvent = new ManualResetEventSlim(false);
        private volatile bool _deleteFile;

        private PTable(string filename, 
                       Guid id, 
                       int bufferSize = DefaultSequentialBufferSize, 
                       int initialReaders = ESConsts.PTableInitialReaderCount, 
                       int maxReaders = ESConsts.PTableMaxReaderCount, 
                       int depth = 16)
        {
            Ensure.NotNullOrEmpty(filename, "filename");
            Ensure.NotEmptyGuid(id, "id");
            Ensure.Positive(maxReaders, "maxReaders");
            Ensure.Positive(bufferSize, "bufferSize");
            Ensure.Nonnegative(depth, "depth");

            if (!File.Exists(filename)) 
                throw new CorruptIndexException(new PTableNotFoundException(filename));

            _id = id;
            _bufferSize = bufferSize;
            _filename = filename;
            _size = new FileInfo(_filename).Length - PTableHeader.Size - MD5Size;
            File.SetAttributes(_filename, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);

            _workItems = new ObjectPool<WorkItem>(string.Format("PTable {0} work items", _id),
                                                  initialReaders,
                                                  maxReaders,
                                                  () => new WorkItem(filename, DefaultBufferSize),
                                                  workItem => workItem.Dispose(),
                                                  pool => OnAllWorkItemsDisposed());

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
                _midpoints = CacheMidpoints(depth);
            }
            catch (PossibleToHandleOutOfMemoryException)
            {
                Log.Error("Was unable to create midpoints for PTable. Performance hit possible. OOM Exception.");
            }
        }

        internal Midpoint[] CacheMidpoints(int depth)
        {
            if (depth < 0 || depth > 30)
                throw new ArgumentOutOfRangeException("depth");

            if (Count == 0 || depth == 0)
                return null;

            var workItem = GetWorkItem();
            try
            {
                int midpointsCount;
                Midpoint[] midpoints;
                try
                {
                    midpointsCount = Math.Max(2, Math.Min(1 << depth, Count));
                    midpoints = new Midpoint[midpointsCount];
                }
                catch (OutOfMemoryException exc)
                {
                    throw new PossibleToHandleOutOfMemoryException("Failed to allocate memory for Midpoint cache.", exc);
                }

                for (int k = 0; k < midpointsCount; ++k)
                {
                    int index = (int)((long)k * (Count - 1) / (midpointsCount - 1));
                    midpoints[k] = new Midpoint(ReadEntry(index, workItem).Key, index);
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
                
                if (hash == null)
                    throw new CorruptIndexException(new HashValidationException("Calculated MD5 hash is null!"));
                if (fileHash.Length != hash.Length)
                    throw new CorruptIndexException(new HashValidationException(
                        string.Format("Hash sizes differ! FileHash({0}): {1}, hash({2}): {3}.",
                                      fileHash.Length, BitConverter.ToString(fileHash),
                                      hash.Length, BitConverter.ToString(hash))));

                for (int i = 0; i < fileHash.Length; i++)
                {
                    if (fileHash[i] != hash[i])
                        throw new CorruptIndexException(new HashValidationException(
                            string.Format("Hashes are different! FileHash: {0}, hash: {1}.",
                                          BitConverter.ToString(fileHash), BitConverter.ToString(hash))));
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
            try
            {
                return _workItems.Get();
            }
            catch (ObjectPoolDisposingException)
            {
                throw new FileBeingDeletedException();
            }
            catch (ObjectPoolMaxLimitReachedException)
            {
                throw new Exception("Unable to acquire work item.");
            }
        }

        private void ReturnWorkItem(WorkItem workItem)
        {
            _workItems.Return(workItem);
        }

        public void MarkForDestruction()
        {
            _deleteFile = true;
            _workItems.MarkForDisposal();
        }

        public void Dispose()
        {
            _deleteFile = false;
            _workItems.MarkForDisposal();
        }

        private void OnAllWorkItemsDisposed()
        {
            File.SetAttributes(_filename, FileAttributes.Normal);
            if (_deleteFile)
                File.Delete(_filename);
            _destroyEvent.Set();
        }

        public void WaitForDisposal(int timeout)
        {
            if (!_destroyEvent.Wait(timeout))
                throw new TimeoutException();
        }

        public void WaitForDisposal(TimeSpan timeout)
        {
            if (!_destroyEvent.Wait(timeout))
                throw new TimeoutException();
        }

        internal struct Midpoint
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
            public readonly BinaryReader Reader;

            public WorkItem(string filename, int bufferSize)
            {
                Stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.RandomAccess);
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
                }
            }
        }
    }
}
