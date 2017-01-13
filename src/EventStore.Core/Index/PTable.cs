using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Exceptions;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Unbuffered;

namespace EventStore.Core.Index
{
    public enum FileType: byte
    {
        PTableFile = 1,
        ChunkFile = 2
    }

    public class PTableVersions
    {
        public const byte Index32Bit = 1;
        public const byte Index64Bit = 2;
    }

    public partial class PTable : ISearchTable, IDisposable
    {
        public const int IndexEntry32Size = sizeof(int) + sizeof(int) + sizeof(long);
        public const int IndexEntry64Size = sizeof(int) + sizeof(long) + sizeof(long);
        public const int IndexKey32Size = sizeof(int) + sizeof(int);
        public const int IndexKey64Size = sizeof(int) + sizeof(long);
        public const int MD5Size = 16;
        public const int DefaultBufferSize = 8192;
        public const int DefaultSequentialBufferSize = 65536;

        private static readonly ILogger Log = LogManager.GetLoggerFor<PTable>();

        public Guid Id { get { return _id; } }
        public long Count { get { return _count; } }
        public string Filename { get { return _filename; } }
        public byte Version { get { return _version; } }

        private readonly Guid _id;
        private readonly string _filename;
        private readonly long _count;
        private readonly long _size;
        private readonly Midpoint[] _midpoints;
        private readonly IndexEntryKey _minEntry, _maxEntry;
        private readonly ObjectPool<WorkItem> _workItems;
        private readonly byte _version;
        private readonly int _indexEntrySize;
        private readonly int _indexKeySize;

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
                if ((header.Version != PTableVersions.Index32Bit) &&
                    (header.Version != PTableVersions.Index64Bit))
                    throw new CorruptIndexException(new WrongFileVersionException(_filename, header.Version, Version));
                _version = header.Version;

                if (_version == PTableVersions.Index32Bit)
                {
                    _indexEntrySize = IndexEntry32Size;
                    _indexKeySize = IndexKey32Size;
                }
                if (_version == PTableVersions.Index64Bit)
                {
                    _indexEntrySize = IndexEntry64Size;
                    _indexKeySize = IndexKey64Size;
                }
                _count = ((_size - PTableHeader.Size - MD5Size) / _indexEntrySize);

                if (Count == 0)
                {
                    _minEntry = new IndexEntryKey(ulong.MaxValue, int.MaxValue);
                    _maxEntry = new IndexEntryKey(ulong.MinValue, int.MinValue);
                }
                else
                {
                    var minEntry = ReadEntry(_indexEntrySize, Count - 1, readerWorkItem, _version);
                    _minEntry = new IndexEntryKey(minEntry.Stream, minEntry.Version);
                    var maxEntry = ReadEntry(_indexEntrySize, 0, readerWorkItem, _version);
                    _maxEntry = new IndexEntryKey(maxEntry.Stream, maxEntry.Version);
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
            Log.Trace("Loading PTable (Version: {0}) '{1}' ({2} entries, cache depth {3}) done in {4}.",
                      _version, Path.GetFileName(Filename), Count, calcdepth, sw.Elapsed);
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
#if  MONO
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
                        var previousKey = new IndexEntryKey(long.MaxValue, int.MaxValue);
                        for (long k = 0; k < midpointsCount; ++k)
                        {
                            var nextIndex = (long)k * (count - 1) / (midpointsCount - 1);
                            if (previousNextIndex != nextIndex) {
                                ReadUntilWithMd5(PTableHeader.Size + _indexEntrySize * nextIndex, stream, md5);
                                stream.Read(buffer, 0, _indexKeySize);
                                md5.TransformBlock(buffer, 0, _indexKeySize, null, 0);
                                IndexEntryKey key;
                                if (_version == PTableVersions.Index32Bit)
                                {
                                    key = new IndexEntryKey(BitConverter.ToUInt32(buffer, 4), BitConverter.ToInt32(buffer, 0));
                                }
                                else
                                {
                                    key = new IndexEntryKey(BitConverter.ToUInt64(buffer, 4), BitConverter.ToInt32(buffer, 0));
                                }
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
#if MONO
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
                    yield return ReadNextNoSeek(workItem, _version);
                }
            }
            finally
            {
                ReturnWorkItem(workItem);
            }
        }

        public bool TryGetOneValue(ulong stream, int number, out long position)
        {
            IndexEntry entry;
            var hash = GetHash(stream);
            if (TryGetLargestEntry(hash, number, number, out entry))
            {
                position = entry.Position;
                return true;
            }
            position = -1;
            return false;
        }

        public bool TryGetLatestEntry(ulong stream, out IndexEntry entry)
        {
            ulong hash = GetHash(stream);
            return TryGetLargestEntry(hash, 0, int.MaxValue, out entry);
        }

        private bool TryGetLargestEntry(ulong stream, int startNumber, int endNumber, out IndexEntry entry)
        {
            Ensure.Nonnegative(startNumber, "startNumber");
            Ensure.Nonnegative(endNumber, "endNumber");

            entry = TableIndex.InvalidIndexEntry;

            var startKey = BuildKey(stream, startNumber);
            var endKey = BuildKey(stream, endNumber);

            if (startKey.GreaterThan(_maxEntry) || endKey.SmallerThan(_minEntry))
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
                    IndexEntry midpoint = ReadEntry(_indexEntrySize, mid, workItem, _version);
                    var midpointKey = new IndexEntryKey(midpoint.Stream, midpoint.Version);
                    if (midpointKey.GreaterThan(endKey))
                        low = mid + 1;
                    else
                        high = mid;
                }

                var candEntry = ReadEntry(_indexEntrySize, high, workItem, _version);
                var candKey = new IndexEntryKey(candEntry.Stream, candEntry.Version);
                if (candKey.GreaterThan(endKey))
                    throw new Exception(string.Format("candEntry ({0}@{1}) > startKey {2}, stream {3}, startNum {4}, endNum {5}, PTable: {6}.", candEntry.Stream, candEntry.Version, startKey, stream, startNumber, endNumber, Filename));
                if (candKey.SmallerThan(startKey))
                    return false;
                entry = candEntry;
                return true;
            }
            finally
            {
                ReturnWorkItem(workItem);
            }
        }

        public bool TryGetOldestEntry(ulong stream, out IndexEntry entry)
        {
            ulong hash = GetHash(stream);
            return TryGetSmallestEntry(hash, 0, int.MaxValue, out entry);
        }

        private bool TryGetSmallestEntry(ulong stream, int startNumber, int endNumber, out IndexEntry entry)
        {
            Ensure.Nonnegative(startNumber, "startNumber");
            Ensure.Nonnegative(endNumber, "endNumber");

            entry = TableIndex.InvalidIndexEntry;

            var startKey = BuildKey(stream, startNumber);
            var endKey = BuildKey(stream, endNumber);

            if (startKey.GreaterThan(_maxEntry) || endKey.SmallerThan(_minEntry))
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
                    var midpoint = ReadEntry(_indexEntrySize, mid, workItem, _version);
                    var midpointKey = new IndexEntryKey(midpoint.Stream, midpoint.Version);
                    if (midpointKey.SmallerThan(startKey))
                        high = mid - 1;
                    else
                        low = mid;
                }

                var candEntry = ReadEntry(_indexEntrySize, high, workItem, _version);
                var candidateKey = new IndexEntryKey(candEntry.Stream, candEntry.Version);
                if (candidateKey.SmallerThan(startKey))
                    throw new Exception(string.Format("candEntry ({0}@{1}) < startKey {2}, stream {3}, startNum {4}, endNum {5}, PTable: {6}.", candEntry.Stream, candEntry.Version, startKey, stream, startNumber, endNumber, Filename));
                if (candidateKey.GreaterThan(endKey))
                    return false;
                entry = candEntry;
                return true;
            }
            finally
            {
                ReturnWorkItem(workItem);
            }
        }

        public IEnumerable<IndexEntry> GetRange(ulong stream, int startNumber, int endNumber, int? limit = null)
        {
            Ensure.Nonnegative(startNumber, "startNumber");
            Ensure.Nonnegative(endNumber, "endNumber");

            ulong hash = GetHash(stream);

            var result = new List<IndexEntry>();
            var startKey = BuildKey(hash, startNumber);
            var endKey = BuildKey(hash, endNumber);

            if (startKey.GreaterThan(_maxEntry) || endKey.SmallerThan(_minEntry))
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
                    IndexEntry midpoint = ReadEntry(_indexEntrySize, mid, workItem, _version);
                    var midpointKey = new IndexEntryKey(midpoint.Stream, midpoint.Version);
                    if (midpointKey.SmallerEqualsThan(endKey))
                        high = mid;
                    else
                        low = mid + 1;
                }

                PositionAtEntry(_indexEntrySize, high, workItem);
                for (long i=high, n=Count; i<n; ++i)
                {
                    IndexEntry entry = ReadNextNoSeek(workItem, _version);
                    var candidateKey = new IndexEntryKey(entry.Stream, entry.Version);
                    if (candidateKey.GreaterThan(endKey))
                        throw new Exception(string.Format("entry ({0}@{1}) > endKey {2}, stream {3}, startNum {4}, endNum {5}, PTable: {6}.", entry.Stream, entry.Version, startKey, stream, startNumber, endNumber, Filename));
                    if (candidateKey.SmallerThan(startKey))
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

        private ulong GetHash(ulong hash){
            return _version == PTableVersions.Index32Bit ? hash >> 32 : hash;
        }

        private static IndexEntryKey BuildKey(ulong stream, int version)
        {
            return new IndexEntryKey(stream, version);
        }

        private Range LocateRecordRange(IndexEntryKey key)
        {
            var midpoints = _midpoints;
            if (midpoints == null)
                return new Range(0, Count - 1);
            long lowerMidpoint = LowerMidpointBound(midpoints, key);
            long upperMidpoint = UpperMidpointBound(midpoints, key);
            return new Range(midpoints[lowerMidpoint].ItemIndex, midpoints[upperMidpoint].ItemIndex);
        }

        private long LowerMidpointBound(Midpoint[] midpoints, IndexEntryKey key)
        {
            long l = 0;
            long r = midpoints.Length - 1;
            while (l < r)
            {
                long m = l + (r - l + 1) / 2;
                if (midpoints[m].Key.GreaterThan(key))
                    l = m;
                else
                    r = m - 1;
            }
            return l;
        }

        private long UpperMidpointBound(Midpoint[] midpoints, IndexEntryKey key)
        {
            long l = 0;
            long r = midpoints.Length - 1;
            while (l < r)
            {
                long m = l + (r - l) / 2;
                if (midpoints[m].Key.SmallerThan(key))
                    r = m;
                else
                    l = m + 1;
            }
            return r;
        }

        private static void PositionAtEntry(int indexEntrySize, long indexNum, WorkItem workItem)
        {
            workItem.Stream.Seek(indexEntrySize * indexNum + PTableHeader.Size, SeekOrigin.Begin);
        }

        private static IndexEntry ReadEntry(int indexEntrySize, long indexNum, WorkItem workItem, int ptableVersion)
        {
            long seekTo = indexEntrySize * indexNum + PTableHeader.Size;
            workItem.Stream.Seek(seekTo, SeekOrigin.Begin);
            return ReadNextNoSeek(workItem, ptableVersion);
        }

        private static IndexEntry ReadNextNoSeek(WorkItem workItem, int ptableVersion)
        {
            int version = workItem.Reader.ReadInt32();
            ulong stream = ptableVersion == PTableVersions.Index32Bit ? workItem.Reader.ReadUInt32() : workItem.Reader.ReadUInt64();
            long position = workItem.Reader.ReadInt64();
            return new IndexEntry(stream, version, position);
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
            public readonly IndexEntryKey Key;
            public readonly long ItemIndex;

            public Midpoint(IndexEntryKey key, long itemIndex)
            {
                Key = key;
                ItemIndex = itemIndex;
            }
        }

        internal struct IndexEntryKey
        {
            public ulong Stream;
            public int Version;
            public IndexEntryKey(ulong stream, int version)
            {
                Stream = stream;
                Version = version;
            }

            public bool GreaterThan(IndexEntryKey other)
            {
                if(Stream == other.Stream)
                {
                    return Version > other.Version;
                }
                return Stream > other.Stream;
            }

            public bool SmallerThan(IndexEntryKey other)
            {
                if(Stream == other.Stream)
                {
                    return Version < other.Version;
                }
                return Stream < other.Stream;
            }

            public bool GreaterEqualsThan(IndexEntryKey other)
            {
                if (Stream == other.Stream)
                {
                    return Version >= other.Version;
                }
                return Stream >= other.Stream;
            }

            public bool SmallerEqualsThan(IndexEntryKey other)
            {
                if(Stream == other.Stream)
                {
                    return Version <= other.Version;
                }
                return Stream <= other.Stream;
            }

            public override string ToString()
            {
                return string.Format("Stream: {0}, Version: {1}", Stream, Version);
            }
        }

        private class WorkItem : IDisposable
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
