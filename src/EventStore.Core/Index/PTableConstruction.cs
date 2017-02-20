using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using EventStore.Common.Utils;

namespace EventStore.Core.Index
{
    public unsafe partial class PTable
    {
        public static PTable FromFile(string filename, int cacheDepth)
        {
            return new PTable(filename, Guid.NewGuid(), depth: cacheDepth);
        }

        public static PTable FromMemtable(IMemTable table, string filename, int cacheDepth = 16)
        {
            Ensure.NotNull(table, "table");
            Ensure.NotNullOrEmpty(filename, "filename");
            Ensure.Nonnegative(cacheDepth, "cacheDepth");

            int indexEntrySize = GetIndexEntrySize(table.Version);

            var sw = Stopwatch.StartNew();
            using (var fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                                           DefaultSequentialBufferSize, FileOptions.SequentialScan))
            {
                fs.SetLength(PTableHeader.Size + indexEntrySize * (long)table.Count + MD5Size); // EXACT SIZE
                fs.Seek(0, SeekOrigin.Begin);

                using (var md5 = MD5.Create())
                using (var cs = new CryptoStream(fs, md5, CryptoStreamMode.Write))
                using (var bs = new BufferedStream(cs, DefaultSequentialBufferSize))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(table.Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    // WRITE INDEX ENTRIES
                    var buffer = new byte[indexEntrySize];
                    foreach (var record in table.IterateAllInOrder())
                    {
                        var rec = record;
                        AppendRecordTo(bs, buffer, table.Version, rec, indexEntrySize);
                    }
                    bs.Flush();
                    cs.FlushFinalBlock();

                    // WRITE MD5
                    var hash = md5.Hash;
                    fs.Write(hash, 0, hash.Length);
                }
            }
            Log.Trace("Dumped MemTable [{0}, {1} entries] in {2}.", table.Id, table.Count, sw.Elapsed);
            return new PTable(filename, table.Id, depth: cacheDepth);
        }

        public static PTable MergeTo(IList<PTable> tables, string outputFile, Func<string, ulong, ulong> upgradeHash, Func<IndexEntry, bool> existsAt, Func<IndexEntry, Tuple<string, bool>> readRecord, byte version, int cacheDepth = 16)
        {
            Ensure.NotNull(tables, "tables");
            Ensure.NotNullOrEmpty(outputFile, "outputFile");
            Ensure.Nonnegative(cacheDepth, "cacheDepth");

            var indexEntrySize = GetIndexEntrySize(version);

            var fileSize = GetFileSize(tables, indexEntrySize); // approximate file size
            if (tables.Count == 2)
                return MergeTo2(tables, fileSize, indexEntrySize, outputFile, upgradeHash, existsAt, readRecord, version, cacheDepth); // special case

            Log.Trace("PTables merge started.");
            var watch = Stopwatch.StartNew();

            var enumerators = tables.Select(table => new EnumerableTable(version, table, upgradeHash, existsAt, readRecord)).ToList();

            for (int i = 0; i < enumerators.Count; i++)
            {
                if (!enumerators[i].MoveNext())
                {
                    enumerators[i].Dispose();
                    enumerators.RemoveAt(i);
                    i--;
                }
            }

            long dumpedEntryCount = 0;
            using (var f = new FileStream(outputFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                                          DefaultSequentialBufferSize, FileOptions.SequentialScan))
            {
                f.SetLength(fileSize);
                f.Seek(0, SeekOrigin.Begin);

                using (var md5 = MD5.Create())
                using (var cs = new CryptoStream(f, md5, CryptoStreamMode.Write))
                using (var bs = new BufferedStream(cs, DefaultSequentialBufferSize))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    var buffer = new byte[indexEntrySize];
                    // WRITE INDEX ENTRIES
                    while (enumerators.Count > 0)
                    {
                        var idx = GetMaxOf(enumerators);
                        var current = enumerators[idx].Current;
                        if(existsAt(current))
                        {
                            AppendRecordTo(bs, buffer, version, current, indexEntrySize);
                            dumpedEntryCount += 1;
                        }
                        if (!enumerators[idx].MoveNext())
                        {
                            enumerators[idx].Dispose();
                            enumerators.RemoveAt(idx);
                        }
                    }
                    bs.Flush();
                    cs.FlushFinalBlock();

                    f.FlushToDisk();
                    f.SetLength(f.Position + MD5Size);

                    // WRITE MD5
                    var hash = md5.Hash;
                    f.Write(hash, 0, hash.Length);
                    f.FlushToDisk();
                }
            }
            Log.Trace("PTables merge finished in {0} ([{1}] entries merged into {2}).",
                      watch.Elapsed, string.Join(", ", tables.Select(x => x.Count)), dumpedEntryCount);
            return new PTable(outputFile, Guid.NewGuid(), depth: cacheDepth);
        }

        private static int GetIndexEntrySize(byte version)
        {
            if (version == PTableVersions.IndexV1)
            {
                return PTable.IndexEntryV1Size;
            }
            if (version == PTableVersions.IndexV2)
            {
                return PTable.IndexEntryV2Size;
            }
            return PTable.IndexEntryV3Size;
        }

        private static PTable MergeTo2(IList<PTable> tables, long fileSize, int indexEntrySize, string outputFile,
                                       Func<string, ulong, ulong> upgradeHash, Func<IndexEntry, bool> existsAt, Func<IndexEntry, Tuple<string, bool>> readRecord, 
                                       byte version, int cacheDepth)
        {
            Log.Trace("PTables merge started (specialized for <= 2 tables).");
            var watch = Stopwatch.StartNew();

            var enumerators = tables.Select(table => new EnumerableTable(version, table, upgradeHash, existsAt, readRecord)).ToList();
            long dumpedEntryCount = 0;
            using (var f = new FileStream(outputFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                                          DefaultSequentialBufferSize, FileOptions.SequentialScan))
            {
                f.SetLength(fileSize);
                f.Seek(0, SeekOrigin.Begin);

                using (var md5 = MD5.Create())
                using (var cs = new CryptoStream(f, md5, CryptoStreamMode.Write))
                using (var bs = new BufferedStream(cs, DefaultSequentialBufferSize))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    // WRITE INDEX ENTRIES
                    var buffer = new byte[indexEntrySize];
                    var enum1 = enumerators[0];
                    var enum2 = enumerators[1];
                    bool available1 = enum1.MoveNext();
                    bool available2 = enum2.MoveNext();
                    IndexEntry current;
                    while (available1 || available2)
                    {
                        var entry1 = new IndexEntry(enum1.Current.Stream, enum1.Current.Version, enum1.Current.Position);
                        var entry2 = new IndexEntry(enum2.Current.Stream, enum2.Current.Version, enum2.Current.Position);

                        if (available1 && (!available2 || entry1.CompareTo(entry2) > 0))
                        {
                            current = entry1;
                            available1 = enum1.MoveNext();
                        }
                        else
                        {
                            current = entry2;
                            available2 = enum2.MoveNext();
                        }

                        if (existsAt(current))
                        {
                            AppendRecordTo(bs, buffer, version, current, indexEntrySize);
                            dumpedEntryCount += 1;
                        }
                    }
                    bs.Flush();
                    cs.FlushFinalBlock();

                    f.SetLength(f.Position + MD5Size);

                    // WRITE MD5
                    var hash = md5.Hash;
                    f.Write(hash, 0, hash.Length);
                    f.FlushToDisk();
                }
            }
            Log.Trace("PTables merge finished in {0} ([{1}] entries merged into {2}).",
                      watch.Elapsed, string.Join(", ", tables.Select(x => x.Count)), dumpedEntryCount);
            return new PTable(outputFile, Guid.NewGuid(), version, depth: cacheDepth);
        }

        private static long GetFileSize(IList<PTable> tables, int indexEntrySize)
        {
            long count = 0;
            for (int i = 0; i < tables.Count; ++i)
            {
                count += tables[i].Count;
            }
            return PTableHeader.Size + indexEntrySize * count + MD5Size;
        }

        private static int GetMaxOf(List<EnumerableTable> enumerators)
        {
            var max = new IndexEntry(ulong.MinValue, 0, long.MinValue);
            int idx = 0;
            for (int i = 0; i < enumerators.Count; i++)
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

        private static void AppendRecordTo(Stream stream, byte[] buffer, byte version, IndexEntry entry, int indexEntrySize)
        {
            var bytes = entry.Bytes;
            if (version == PTableVersions.IndexV1)
            {
                var entryV1 = new IndexEntryV1((uint)entry.Stream, (int)entry.Version, entry.Position);
                bytes = entryV1.Bytes;
            }
            else if (version == PTableVersions.IndexV2)
            {
                var entryV2 = new IndexEntryV2(entry.Stream, (int)entry.Version, entry.Position);
                bytes = entryV2.Bytes;
            }
            Marshal.Copy((IntPtr)bytes, buffer, 0, indexEntrySize);
            stream.Write(buffer, 0, indexEntrySize);
        }

        internal class EnumerableTable : IEnumerator<IndexEntry>
        {
            private ISearchTable _ptable;
            private List<IndexEntry> _list;
            private IEnumerator<IndexEntry> _enumerator;
            readonly IEnumerator<IndexEntry> _ptableEnumerator;
            readonly Func<string, ulong, ulong> _upgradeHash;
            readonly Func<IndexEntry, bool> _existsAt;
            readonly Func<IndexEntry, Tuple<string, bool>> _readRecord;
            readonly byte _mergedPTableVersion;
            static readonly IComparer<IndexEntry> EntryComparer = new IndexEntryComparer();

            public byte GetVersion()
            {
                return _ptable.Version;
            }

            public IndexEntry Current
            {
                get
                {
                    return _enumerator.Current;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return _enumerator.Current;
                }
            }

            public EnumerableTable(byte mergedPTableVersion, ISearchTable table, Func<string, ulong, ulong> upgradeHash, Func<IndexEntry, bool> existsAt, Func<IndexEntry, Tuple<string, bool>> readRecord)
            {
                _mergedPTableVersion = mergedPTableVersion;
                _ptable = table;

                _upgradeHash = upgradeHash;
                _existsAt = existsAt;
                _readRecord = readRecord;

                if(table.Version == PTableVersions.IndexV1 && mergedPTableVersion != PTableVersions.IndexV1)
                {
                    _list = new List<IndexEntry>();
                    _enumerator = _list.GetEnumerator();
                    _ptableEnumerator = _ptable.IterateAllInOrder().GetEnumerator();
                }
                else
                {
                    _enumerator = _ptable.IterateAllInOrder().GetEnumerator();
                }
            }

            public IEnumerator<IndexEntry> GetEnumerator()
            {
                return _enumerator;
            }

            public void Dispose()
            {
                if (_ptableEnumerator != null)
                {
                    _ptableEnumerator.Dispose();
                }
                _enumerator.Dispose();
            }

            public bool MoveNext()
            {
                var hasMovedToNext = _enumerator.MoveNext();
                if (_list == null || hasMovedToNext) return hasMovedToNext;

                _enumerator.Dispose();
                _list = ReadUntilDifferentHash(_mergedPTableVersion, _ptableEnumerator, _upgradeHash, _existsAt, _readRecord);
                _enumerator = _list.GetEnumerator();

                return _enumerator.MoveNext();
            }

            private List<IndexEntry> ReadUntilDifferentHash(byte version, IEnumerator<IndexEntry> ptableEnumerator, Func<string, ulong, ulong> upgradeHash, Func<IndexEntry, bool> existsAt, Func<IndexEntry, Tuple<string, bool>> readRecord)
            {
                var list = new List<IndexEntry>();
                IndexEntry current = new IndexEntry(0, 0, 0); 
                ulong hash = 0;
                while (hash == current.Stream && ptableEnumerator.MoveNext())
                {
                    current = ptableEnumerator.Current;
                    if (existsAt(current))
                    {
                        list.Add(new IndexEntry(upgradeHash(readRecord(current).Item1, current.Stream), current.Version, current.Position));
                    }
                    if (hash == 0) hash = current.Stream;
                }
                list.Sort(EntryComparer);
                return list;
            }

            private class IndexEntryComparer : IComparer<IndexEntry>
            {
                public int Compare(IndexEntry x, IndexEntry y)
                {
                    return -x.CompareTo(y);
                }
            }

            public void Reset()
            {
                _enumerator.Reset();
            }
        }
    }
}
