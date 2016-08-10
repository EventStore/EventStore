using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using EventStore.Common.Log;
using EventStore.Common.Streams;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Exceptions;
using EventStore.Core.Settings;
using EventStore.Core.Util;
using EventStore.Core.TransactionLog.Unbuffered;

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
        public long Count { get { return _count; } }
        public string Filename { get { return _filename; } }

        private readonly Guid _id;
        private readonly string _filename;
        private readonly long _count;
        private readonly long _size;
        private readonly Midpoint[] _midpoints;
        private readonly ulong _minEntry, _maxEntry;
        private readonly ObjectPool<WorkItem> _workItems;

        private readonly ManualResetEventSlim _destroyEvent = new ManualResetEventSlim(false);
        private volatile bool _deleteFile;

        internal Midpoint[] GetMidPoints() {return _midpoints;}

        private PTable(string filename,
                       Guid id,
                       int initialReaders = ESConsts.PTableInitialReaderCount,
                       int maxReaders = ESConsts.PTableMaxReaderCount,
                       int depth = 16)
        {
            Ensure.NotNullOrEmpty(filename, "filename");
            Ensure.NotEmptyGuid(id, "id");
            Ensure.Positive(maxReaders, "maxReaders");
            Ensure.Nonnegative(depth, "depth");

            if (!File.Exists(filename))
                throw new CorruptIndexException(new PTableNotFoundException(filename));

            _id = id;
            _filename = filename;

            Log.Trace("Loading and Verification of PTable '{0}' started...", Path.GetFileName(Filename));
            var sw = Stopwatch.StartNew();
            _size = new FileInfo(_filename).Length;
            _count = ((_size - PTableHeader.Size - MD5Size) / IndexEntrySize);

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

                if (Count == 0)
                {
                    _minEntry = ulong.MaxValue;
                    _maxEntry = ulong.MinValue;
                }
                else
                {
                    _minEntry = ReadEntry(Count - 1, readerWorkItem).Key;
                    _maxEntry = ReadEntry(0, readerWorkItem).Key;
                }
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
            int calcdepth = 0;
            try
            {
                calcdepth = GetDepth(_size, depth);
                _midpoints = CacheMidpointsAndVerifyHash(calcdepth);
            }
            catch (PossibleToHandleOutOfMemoryException)
            {
                Log.Error("Unable to create midpoints for PTable '{0}' ({1} entries, depth {2} requested). "
                          + "Performance hit will occur. OOM Exception.", Path.GetFileName(Filename), Count, depth);
            }
            Log.Trace("Loading PTable '{0}' ({1} entries, cache depth {2}) done in {3}.",
                      Path.GetFileName(Filename), Count, calcdepth, sw.Elapsed);
        }

        private int GetDepth(long fileSize, int minDepth) {
            if((2L << 28) * 4096L < fileSize) return 28;
            for(int i=27;i>minDepth;i--) {
                if((2L << i) * 4096L < fileSize) {
                    return i + 1;
                }
            }
            return minDepth;
        }

        internal Midpoint[] CacheMidpointsAndVerifyHash(int depth)
        {
            var buffer = new byte[4096];
            if (depth < 0 || depth > 30)
                throw new ArgumentOutOfRangeException("depth");
            var count = Count;
            if (count == 0 || depth == 0)
                return null;
#if  __MonoCS__
            var workItem = GetWorkItem();
            var stream = workItem.Stream;
            try {
#else
            using (var stream = UnbufferedFileStream.Create(_filename, FileMode.Open, FileAccess.Read, FileShare.Read, false, 4096, 4096, false, 4096))
            {
#endif
                try {
                    int midpointsCount;
                    Midpoint[] midpoints;
                    using (MD5 md5 = MD5.Create())
                    {
                        try
                        {
                            midpointsCount = (int)Math.Max(2L, Math.Min((long)1 << depth, count));
                            midpoints = new Midpoint[midpointsCount];
                        }
                        catch (OutOfMemoryException exc)
                        {
                            throw new PossibleToHandleOutOfMemoryException("Failed to allocate memory for Midpoint cache.", exc);
                        }
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.Read(buffer, 0, PTableHeader.Size);
                        md5.TransformBlock(buffer, 0, PTableHeader.Size, null, 0);
                        long previousNextIndex = long.MinValue;
                        UInt64 previousKey = long.MaxValue;
                        for (long k = 0; k < midpointsCount; ++k)
                        {
                            var nextIndex = (long)k * (count - 1) / (midpointsCount - 1);
                            if (previousNextIndex != nextIndex) {
                                ReadUntilWithMd5(PTableHeader.Size + IndexEntrySize * nextIndex, stream, md5);
                                stream.Read(buffer, 0, 8);
                                md5.TransformBlock(buffer, 0, 8, null, 0);
                                var key = BitConverter.ToUInt64(buffer, 0);
                                midpoints[k] = new Midpoint(key, nextIndex);
                                previousNextIndex = nextIndex;
                                previousKey = key;
                            } else {
                                midpoints[k] = new Midpoint(previousKey, previousNextIndex);
                            }
                        }

                        ReadUntilWithMd5(stream.Length - MD5Size, stream, md5);
                        //verify hash (should be at stream.length - MD5Size)
                        md5.TransformFinalBlock(Empty.ByteArray, 0, 0);
                        var fileHash = new byte[MD5Size];
                        stream.Read(fileHash, 0, MD5Size);
                        ValidateHash(md5.Hash, fileHash);
                        return midpoints;
                    }
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }
#if __MonoCS__
            finally
            {
                ReturnWorkItem(workItem);
            }
#endif
        }


        private readonly byte[] TmpReadBuf = new byte[DefaultBufferSize];
        private void ReadUntilWithMd5(long nextPos, Stream fileStream, MD5 md5)
        {
            long toRead = nextPos - fileStream.Position;
            if (toRead < 0) throw new Exception("should not do negative reads.");
            while (toRead > 0)
            {
                var localReadCount = Math.Min(toRead, TmpReadBuf.Length);
                int read = fileStream.Read(TmpReadBuf, 0, (int)localReadCount);
                md5.TransformBlock(TmpReadBuf, 0, read, null, 0);
                toRead -= read;
            }
        }
        void ValidateHash(byte [] fromFile, byte[] computed)
        {
            if (computed == null)
                throw new CorruptIndexException(new HashValidationException("Calculated MD5 hash is null!"));
            if (fromFile == null)
                throw new CorruptIndexException(new HashValidationException("Read from file MD5 hash is null!"));

            if (computed.Length != fromFile.Length)
                throw new CorruptIndexException(
                    new HashValidationException(
                        string.Format(
                           "Hash sizes differ! FileHash({0}): {1}, hash({2}): {3}.",
                           computed.Length,
                           BitConverter.ToString(computed),
                           fromFile.Length,
                           BitConverter.ToString(fromFile))));

            for (int i = 0; i < fromFile.Length; i++)
            {
                if (fromFile[i] != computed[i])
                    throw new CorruptIndexException(
                        new HashValidationException(
                            string.Format(
                                "Hashes are different! computed: {0}, hash: {1}.",
                                BitConverter.ToString(computed),
                                BitConverter.ToString(fromFile))));
            }
        }

        public IEnumerable<IndexEntry> IterateAllInOrder()
        {
            var workItem = GetWorkItem();
            try
            {
                workItem.Stream.Position = PTableHeader.Size;
                for (long i = 0, n = Count; i < n; i++)
                {
                    yield return ReadNextNoSeek(workItem);
                }
            }
            finally
            {
                ReturnWorkItem(workItem);
            }
        }

        public bool TryGetOneValue(uint stream, int number, out long position)
        {
            IndexEntry entry;
            if (TryGetLargestEntry(stream, number, number, out entry))
            {
                position = entry.Position;
                return true;
            }
            position = -1;
            return false;
        }

        public bool TryGetLatestEntry(uint stream, out IndexEntry entry)
        {
            return TryGetLargestEntry(stream, 0, int.MaxValue, out entry);
        }

        private bool TryGetLargestEntry(uint stream, int startNumber, int endNumber, out IndexEntry entry)
        {
            Ensure.Nonnegative(startNumber, "startNumber");
            Ensure.Nonnegative(endNumber, "endNumber");

            entry = TableIndex.InvalidIndexEntry;

            var startKey = BuildKey(stream, startNumber);
            var endKey = BuildKey(stream, endNumber);

            //if (_midpoints != null && (startKey > _midpoints[0].Key || endKey < _midpoints[_midpoints.Length - 1].Key))
            if (startKey > _maxEntry || endKey < _minEntry)
                return false;

            var workItem = GetWorkItem();
            try
            {
                var recordRange = LocateRecordRange(endKey);

                long low = recordRange.Lower;
                long high = recordRange.Upper;
                while (low < high)
                {
                    var mid = low + (high - low) / 2;
                    IndexEntry midpoint = ReadEntry(mid, workItem);
                    if (midpoint.Key > endKey)
                        low = mid + 1;
                    else
                        high = mid;
                }

                var candEntry = ReadEntry(high, workItem);
                if (candEntry.Key > endKey)
                    throw new Exception(string.Format("candEntry.Key {0} > startKey {1}, stream {2}, startNum {3}, endNum {4}, PTable: {5}.", candEntry.Key, startKey, stream, startNumber, endNumber, Filename));
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

        public bool TryGetOldestEntry(uint stream, out IndexEntry entry)
        {
            return TryGetSmallestEntry(stream, 0, int.MaxValue, out entry);
        }

        private bool TryGetSmallestEntry(uint stream, int startNumber, int endNumber, out IndexEntry entry)
        {
            Ensure.Nonnegative(startNumber, "startNumber");
            Ensure.Nonnegative(endNumber, "endNumber");

            entry = TableIndex.InvalidIndexEntry;

            var startKey = BuildKey(stream, startNumber);
            var endKey = BuildKey(stream, endNumber);

            if (startKey > _maxEntry || endKey < _minEntry)
                return false;

            var workItem = GetWorkItem();
            try
            {
                var recordRange = LocateRecordRange(startKey);

                long low = recordRange.Lower;
                long high = recordRange.Upper;
                while (low < high)
                {
                    var mid = low + (high - low + 1) / 2;
                    IndexEntry midpoint = ReadEntry(mid, workItem);
                    if (midpoint.Key < startKey)
                        high = mid - 1;
                    else
                        low = mid;
                }

                var candEntry = ReadEntry(high, workItem);
                if (candEntry.Key < startKey)
                    throw new Exception(string.Format("candEntry.Key {0} < startKey {1}, stream {2}, startNum {3}, endNum {4}, PTable: {5}.", candEntry.Key, startKey, stream, startNumber, endNumber, Filename));
                if (candEntry.Key > endKey)
                    return false;
                entry = candEntry;
                return true;
            }
            finally
            {
                ReturnWorkItem(workItem);
            }
        }

        public IEnumerable<IndexEntry> GetRange(uint stream, int startNumber, int endNumber, int? limit = null)
        {
            Ensure.Nonnegative(startNumber, "startNumber");
            Ensure.Nonnegative(endNumber, "endNumber");

            var result = new List<IndexEntry>();
            var startKey = BuildKey(stream, startNumber);
            var endKey = BuildKey(stream, endNumber);

            //if (_midpoints != null && (startKey > _midpoints[0].Key || endKey < _midpoints[_midpoints.Length - 1].Key))
            if (startKey > _maxEntry || endKey < _minEntry)
                return result;

            var workItem = GetWorkItem();
            try
            {
                var recordRange = LocateRecordRange(endKey);
                long low = recordRange.Lower;
                long high = recordRange.Upper;
                while (low < high)
                {
                    var mid = low + (high - low) / 2;
                    IndexEntry midpoint = ReadEntry(mid, workItem);
                    if (midpoint.Key <= endKey)
                        high = mid;
                    else
                        low = mid + 1;
                }

                PositionAtEntry(high, workItem);
                for (long i=high, n=Count; i<n; ++i)
                {
                    IndexEntry entry = ReadNextNoSeek(workItem);
                    if (entry.Key > endKey)
                        throw new Exception(string.Format("entry.Key {0} > endKey {1}, stream {2}, startNum {3}, endNum {4}, PTable: {5}.", entry.Key, endKey, stream, startNumber, endNumber, Filename));
                    if (entry.Key < startKey)
                        return result;
                    result.Add(entry);
                    if (result.Count == limit) break;
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

        private Range LocateRecordRange(ulong stream)
        {
            var midpoints = _midpoints;
            if (midpoints == null)
                return new Range(0, Count-1);
            long lowerMidpoint = LowerMidpointBound(midpoints, stream);
            long upperMidpoint = UpperMidpointBound(midpoints, stream);
            return new Range(midpoints[lowerMidpoint].ItemIndex, midpoints[upperMidpoint].ItemIndex);
        }

        private long LowerMidpointBound(Midpoint[] midpoints, ulong stream)
        {
            long l = 0;
            long r = midpoints.Length - 1;
            while (l < r)
            {
                long m = l + (r - l + 1) / 2;
                if (midpoints[m].Key > stream)
                    l = m;
                else
                    r = m - 1;
            }
            return l;
        }

        private long UpperMidpointBound(Midpoint[] midpoints, ulong stream)
        {
            long l = 0;
            long r = midpoints.Length - 1;
            while (l < r)
            {
                long m = l + (r - l) / 2;
                if (midpoints[m].Key < stream)
                    r = m;
                else
                    l = m + 1;
            }
            return r;
        }

        private static void PositionAtEntry(long indexNum, WorkItem workItem)
        {
            workItem.Stream.Seek(IndexEntrySize * indexNum + PTableHeader.Size, SeekOrigin.Begin);
        }

        private static IndexEntry ReadEntry(long indexNum, WorkItem workItem)
        {
            long seekTo = IndexEntrySize * indexNum + PTableHeader.Size;
            workItem.Stream.Seek(seekTo, SeekOrigin.Begin);
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
            public readonly long ItemIndex;

            public Midpoint(ulong key, long itemIndex)
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
