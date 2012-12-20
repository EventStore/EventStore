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
using EventStore.Common.Utils;
using EventStore.Core.Data;

namespace EventStore.Core.Index
{
    public unsafe partial class PTable
    {
        public static PTable FromFile(string filename)
        {
            return new PTable(filename, Guid.NewGuid());
        }

        public static PTable FromMemtable(IMemTable table, string filename, int cacheDepth = 16)
        {
            Ensure.NotNull(table, "table");
            Ensure.NotNullOrEmpty(filename, "filename");
            Ensure.Nonnegative(cacheDepth, "cacheDepth");

            Log.Trace("Started dumping MemTable [{0}] into PTable...", table.Id);
            var sw = Stopwatch.StartNew();
            using (var fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 8096, FileOptions.SequentialScan))
            {
                fs.SetLength(PTableHeader.Size + IndexEntrySize * (long)table.Count + MD5Size); // EXACT SIZE
                fs.Seek(0, SeekOrigin.Begin);

                using (var md5 = MD5.Create())
                using (var cs = new CryptoStream(fs, md5, CryptoStreamMode.Write))
                using (var bs = new BufferedStream(cs, 65536))
                using (var writer = new BinaryWriter(bs))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    var buffer = new byte[IndexEntrySize];
                    // WRITE INDEX ENTRIES
                    foreach (var record in table.IterateAllInOrder())
                    {
                        var rec = record;
                        AppendRecordTo(bs, rec.Bytes, buffer);
                        //AppendRecordTo(writer, record);
                    }
                    bs.Flush();
                    cs.FlushFinalBlock();

                    // WRITE MD5
                    var hash = md5.Hash;
                    fs.Write(hash, 0, hash.Length);
                }
            }
            Log.Trace("Done dumping MemTable [{0}] in {1}.", table.Id, sw.Elapsed);
            return new PTable(filename, table.Id, depth: cacheDepth);
        }

        public static PTable MergeTo(IList<PTable> tables,
                                     string outputFile,
                                     Func<IndexEntry, bool> isHashCollision,
                                     int cacheDepth = 16)
        {
            Ensure.NotNull(tables, "tables");
            Ensure.NotNullOrEmpty(outputFile, "outputFile");
            Ensure.NotNull(isHashCollision, "isHashCollision");
            Ensure.Nonnegative(cacheDepth, "cacheDepth");

            var enumerators = tables.Select(table => table.IterateAllInOrder().GetEnumerator()).ToList();
            var fileSize = GetFileSize(tables); // approximate file size
            if (enumerators.Count == 2)
                return MergeTo2(enumerators, fileSize, outputFile, isHashCollision, cacheDepth); // special case

            Log.Trace("PTables merge started.");
            var watch = Stopwatch.StartNew();

            for (int i = 0; i < enumerators.Count; i++)
            {
                if (!enumerators[i].MoveNext())
                {
                    enumerators[i].Dispose();
                    enumerators.RemoveAt(i);
                    i--;
                }
            }

            using (var f = new FileStream(outputFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
            {
                f.SetLength(fileSize);
                f.Seek(0, SeekOrigin.Begin);

                using (var md5 = MD5.Create())
                using (var cs = new CryptoStream(f, md5, CryptoStreamMode.Write))
                using (var bs = new BufferedStream(cs, 65536))
                using (var writer = new BinaryWriter(bs))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    var bytes = new byte[IndexEntrySize];
                    uint lastDeleted = uint.MaxValue;
                    // WRITE INDEX ENTRIES
                    while (enumerators.Count > 0)
                    {
                        var idx = GetMaxOf(enumerators);
                        var current = enumerators[idx].Current;
                        if (current.Version == EventNumber.DeletedStream && !isHashCollision(current))
                        {
                            lastDeleted = current.Stream;
                            AppendRecordTo(bs, current.Bytes, bytes);
                            //AppendRecordTo(writer, current);
                        }
                        else
                        {
                            if (lastDeleted != current.Stream || current.Version == 0) // we keep 0th event for hash collision detection
                                AppendRecordTo(bs, current.Bytes, bytes);
                                //AppendRecordTo(writer, current);
                        }
                        if (!enumerators[idx].MoveNext())
                        {
                            enumerators[idx].Dispose();
                            enumerators.RemoveAt(idx);
                        }
                    }
                    bs.Flush();
                    cs.FlushFinalBlock();

                    f.SetLength(f.Position + MD5Size);

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

            using (var f = new FileStream(outputFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
            {
                f.SetLength(fileSize);
                f.Seek(0, SeekOrigin.Begin);

                using (var md5 = MD5.Create())
                using (var cs = new CryptoStream(f, md5, CryptoStreamMode.Write))
                using (var bs = new BufferedStream(cs, 65536))
                using (var writer = new BinaryWriter(bs))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    // WRITE INDEX ENTRIES
                    var bytes = new byte[IndexEntrySize];
                    uint lastDeleted = uint.MaxValue;
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

                        if (current.Version == EventNumber.DeletedStream && !isHashCollision(current))
                        {
                            lastDeleted = current.Stream;
                            AppendRecordTo(bs, current.Bytes, bytes);
                            //AppendRecordTo(writer, current);
                        }
                        else
                        {
                            if (lastDeleted != current.Stream || current.Version == 0) // we keep 0th event for hash collision detection
                                AppendRecordTo(bs, current.Bytes, bytes);
                                //AppendRecordTo(writer, current);
                        }
                    }
                    bs.Flush();
                    cs.FlushFinalBlock();

                    f.SetLength(f.Position + MD5Size);

                    // WRITE MD5
                    var hash = md5.Hash;
                    f.Write(hash, 0, hash.Length);
                }
            }
            Log.Trace("PTables merge finished in " + watch.Elapsed);
            return new PTable(outputFile, Guid.NewGuid(), depth: cacheDepth);
        }

        private static long GetFileSize(IList<PTable> tables)
        {
            long count = 0;
            for (int i = 0; i < tables.Count; ++i)
            {
                count += tables[i].Count;
            }
            return PTableHeader.Size + IndexEntrySize * count + MD5Size;
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
