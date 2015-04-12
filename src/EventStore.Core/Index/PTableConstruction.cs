using System;
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

            //Log.Trace("Started dumping MemTable [{0}] into PTable...", table.Id);
            var sw = Stopwatch.StartNew();
            using (var fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                                           DefaultSequentialBufferSize, FileOptions.SequentialScan))
            {
                fs.SetLength(PTableHeader.Size + IndexEntrySize * (long)table.Count + MD5Size); // EXACT SIZE
                fs.Seek(0, SeekOrigin.Begin);

                using (var md5 = MD5.Create())
                using (var cs = new CryptoStream(fs, md5, CryptoStreamMode.Write))
                using (var bs = new BufferedStream(cs, DefaultSequentialBufferSize))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    // WRITE INDEX ENTRIES
                    var buffer = new byte[IndexEntrySize];
                    foreach (var record in table.IterateAllInOrder())
                    {
                        var rec = record;
                        AppendRecordTo(bs, rec.Bytes, buffer);
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

        public static PTable MergeTo(IList<PTable> tables, string outputFile, Func<IndexEntry, bool> recordExistsAt, int cacheDepth = 16)
        {
            Ensure.NotNull(tables, "tables");
            Ensure.NotNullOrEmpty(outputFile, "outputFile");
            Ensure.Nonnegative(cacheDepth, "cacheDepth");

            var fileSize = GetFileSize(tables); // approximate file size
            if (tables.Count == 2)
                return MergeTo2(tables, fileSize, outputFile, recordExistsAt, cacheDepth); // special case

            Log.Trace("PTables merge started.");
            var watch = Stopwatch.StartNew();

            var enumerators = tables.Select(table => table.IterateAllInOrder().GetEnumerator()).ToList();
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
                    var headerBytes = new PTableHeader(Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    var buffer = new byte[IndexEntrySize];
                    // WRITE INDEX ENTRIES
                    while (enumerators.Count > 0)
                    {
                        var idx = GetMaxOf(enumerators);
                        var current = enumerators[idx].Current;
                        if (recordExistsAt(current))
                        {
                            AppendRecordTo(bs, current.Bytes, buffer);
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

        private static PTable MergeTo2(IList<PTable> tables, long fileSize, string outputFile,
                                       Func<IndexEntry, bool> recordExistsAt, int cacheDepth)
        {
            Log.Trace("PTables merge started (specialized for <= 2 tables).");
            var watch = Stopwatch.StartNew();

            var enumerators = tables.Select(table => table.IterateAllInOrder().GetEnumerator()).ToList();
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
                    var headerBytes = new PTableHeader(Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    // WRITE INDEX ENTRIES
                    var buffer = new byte[IndexEntrySize];
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

                        if (recordExistsAt(current))
                        {
                            AppendRecordTo(bs, current.Bytes, buffer);
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
            return new PTable(outputFile, Guid.NewGuid(), depth: cacheDepth);
        }

        private static long GetFileSize(IList<PTable> tables)
        {
            long count = 0;
            for (int i = 0; i < tables.Count; ++i)
            {
                count += tables[i].Count;
            }
            return PTableHeader.Size + IndexEntrySize*count + MD5Size;
        }

        private static int GetMaxOf(List<IEnumerator<IndexEntry>> enumerators)
        {
            //TODO GFY IF WE LIMIT THIS TO FOUR WE CAN UNROLL THIS LOOP AND WILL BE FASTER
            var max = new IndexEntry(ulong.MinValue, long.MinValue);
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

        private static void AppendRecordTo(Stream stream, byte* bytes, byte[] buffer)
        {
            Marshal.Copy((IntPtr)bytes, buffer, 0, IndexEntrySize);
            stream.Write(buffer, 0, IndexEntrySize);
        }

/*
        private static void AppendRecordTo(BinaryWriter writer, IndexEntry indexEntry)
        {
            writer.Write(indexEntry.Key);
            writer.Write(indexEntry.Position);
        }
*/
    }
}
