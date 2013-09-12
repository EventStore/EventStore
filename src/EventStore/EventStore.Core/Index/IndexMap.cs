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
using System.IO;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog;
using EventStore.Core.Util;

namespace EventStore.Core.Index
{
    public class IndexMap
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<IndexMap>();

        public const int IndexMapVersion = 1;

        public readonly int Version;

        public readonly long PrepareCheckpoint;
        public readonly long CommitCheckpoint;

        private readonly List<List<PTable>> _map;
        private readonly int _maxTablesPerLevel;

        private IndexMap(int version, List<List<PTable>> tables, long prepareCheckpoint, long commitCheckpoint, int maxTablesPerLevel)
        {
            Ensure.Nonnegative(version, "version");
            if (prepareCheckpoint < -1) throw new ArgumentOutOfRangeException("prepareCheckpoint");
            if (commitCheckpoint < -1) throw new ArgumentOutOfRangeException("commitCheckpoint");
            if (maxTablesPerLevel <= 1) throw new ArgumentOutOfRangeException("maxTablesPerLevel");

            Version = version;

            PrepareCheckpoint = prepareCheckpoint;
            CommitCheckpoint = commitCheckpoint;

            _map = CopyFrom(tables);
            _maxTablesPerLevel = maxTablesPerLevel;
            
            VerifyStructure();
        }

        private static List<List<PTable>> CopyFrom(List<List<PTable>> tables)
        {
            var tmp = new List<List<PTable>>();
            for (int i = 0; i < tables.Count; i++)
            {
                tmp.Add(new List<PTable>(tables[i]));
            }
            return tmp;
        }

        private void VerifyStructure()
        {
            if (_map.SelectMany(level => level).Any(item => item == null))
                throw new CorruptIndexException("Internal indexmap structure corruption.");
        }

        private static void CreateIfNeeded(int level, List<List<PTable>> tables)
        {
            while (level >= tables.Count)
            {
                tables.Add(new List<PTable>());
            }
            if (tables[level] == null)
                tables[level] = new List<PTable>();
        }

        public IEnumerable<PTable> InOrder()
        {
            var map = _map;
            // level 0 (newest tables) -> N (oldest tables)
            for (int i = 0; i < map.Count; ++i)
            {
                // last in the level's list (newest on level) -> to first (oldest on level)
                for (int j = map[i].Count - 1; j >= 0; --j)
                {
                    yield return map[i][j];
                }
            }
        }

        public IEnumerable<PTable> InReverseOrder()
        {
            var map = _map;
            // N (oldest tables) -> level 0 (newest tables)
            for (int i = map.Count-1; i >= 0; --i)
            {
                // from first (oldest on level) in the level's list -> last in the level's list (newest on level)
                for (int j = 0, n=map[i].Count; j < n; ++j)
                {
                    yield return map[i][j];
                }
            }
        }

        public IEnumerable<string> GetAllFilenames()
        {
            return from level in _map
                   from table in level
                   select table.Filename;
        }

        public static IndexMap FromFile(string filename, int maxTablesPerLevel = 4, bool loadPTables = true)
        {
            if (!File.Exists(filename))
                return new IndexMap(IndexMapVersion, new List<List<PTable>>(), -1, -1, maxTablesPerLevel);

            using (var f = File.OpenRead(filename))
            {
                // calculate real MD5 hash except first 32 bytes which are string representation of stored hash
                f.Position = 32;
                var realHash = MD5Hash.GetHashFor(f);
                f.Position = 0;

                using (var reader = new StreamReader(f))
                {
                    ReadAndCheckHash(reader, realHash);

                    // at this point we can assume the format is ok, so actually no need to check errors.
                    var version = ReadVersion(reader);
                    var checkpoints = ReadCheckpoints(reader);
                    var prepareCheckpoint = checkpoints.PreparePosition;
                    var commitCheckpoint = checkpoints.CommitPosition;

                    var tables = loadPTables ? LoadPTables(reader, filename, checkpoints) : new List<List<PTable>>();

                    if (!loadPTables && reader.ReadLine() != null)
                        throw new CorruptIndexException(
                            string.Format("Negative prepare/commit checkpoint in non-empty IndexMap: {0}.", checkpoints));

                    return new IndexMap(version, tables, prepareCheckpoint, commitCheckpoint, maxTablesPerLevel);
                }
            }
        }

        private static void ReadAndCheckHash(TextReader reader, byte[] realHash)
        {
            // read stored MD5 hash and convert it from string to byte array
            string text;
            if ((text = reader.ReadLine()) == null)
                throw new CorruptIndexException("IndexMap file is empty.");
            if (text.Length != 32 || !text.All(x => char.IsDigit(x) || (x >= 'A' && x <= 'F')))
                throw new CorruptIndexException(string.Format("Corrupted IndexMap MD5 hash. Hash ({0}): {1}.", text.Length, text));

            // check expected and real hashes are the same
            var expectedHash = new byte[16];
            for (int i = 0; i < 16; ++i)
            {
                expectedHash[i] = Convert.ToByte(text.Substring(i*2, 2), 16);
            }
            if (expectedHash.Length != realHash.Length)
            {
                throw new CorruptIndexException(
                        string.Format("Hash validation error (different hash sizes).\n"
                                      + "Expected hash ({0}): {1}, real hash ({2}): {3}.",
                                      expectedHash.Length, BitConverter.ToString(expectedHash),
                                      realHash.Length, BitConverter.ToString(realHash)));
            }
            for (int i = 0; i < realHash.Length; ++i)
            {
                if (expectedHash[i] != realHash[i])
                {
                    throw new CorruptIndexException(
                            string.Format("Hash validation error (different hashes).\n"
                                          + "Expected hash ({0}): {1}, real hash ({2}): {3}.",
                                          expectedHash.Length, BitConverter.ToString(expectedHash),
                                          realHash.Length, BitConverter.ToString(realHash)));
                }
            }
        }

        private static int ReadVersion(TextReader reader)
        {
            string text;
            if ((text = reader.ReadLine()) == null)
                throw new CorruptIndexException("Corrupted version.");
            return int.Parse(text);
        }

        private static TFPos ReadCheckpoints(TextReader reader)
        {
            // read and check prepare/commit checkpoint
            string text;
            if ((text = reader.ReadLine()) == null)
                throw new CorruptIndexException("Corrupted commit checkpoint.");
            try
            {
                long prepareCheckpoint;
                long commitCheckpoint;
                var checkpoints = text.Split('/');
                if (!long.TryParse(checkpoints[0], out prepareCheckpoint) || prepareCheckpoint < -1)
                    throw new CorruptIndexException(string.Format("Invalid prepare checkpoint: {0}.", checkpoints[0]));
                if (!long.TryParse(checkpoints[1], out commitCheckpoint) || commitCheckpoint < -1)
                    throw new CorruptIndexException(string.Format("Invalid commit checkpoint: {0}.", checkpoints[1]));
                return new TFPos(commitCheckpoint, prepareCheckpoint);
            }
            catch (Exception exc)
            {
                throw new CorruptIndexException("Corrupted prepare/commit checkpoints pair.", exc);
            }
        }

        private static List<List<PTable>> LoadPTables(StreamReader reader, string indexmapFilename, TFPos checkpoints)
        {
            var tables = new List<List<PTable>>();

            // all next lines are PTables sorted by levels
            string text;
            while ((text = reader.ReadLine()) != null)
            {
                if (checkpoints.PreparePosition < 0 || checkpoints.CommitPosition < 0)
                    throw new CorruptIndexException(
                        string.Format("Negative prepare/commit checkpoint in non-empty IndexMap: {0}.", checkpoints));

                PTable ptable = null;
                var pieces = text.Split(',');
                try
                {
                    var level = int.Parse(pieces[0]);
                    var position = int.Parse(pieces[1]);
                    var file = pieces[2];
                    var path = Path.GetDirectoryName(indexmapFilename);
                    var ptablePath = Path.Combine(path, file);

                    ptable = PTable.FromFile(ptablePath);
                    ptable.VerifyFileHash();

                    CreateIfNeeded(level, tables);
                    tables[level].Insert(position, ptable);
                }
                catch (Exception exc)
                {
                    // if PTable file path was correct, but data is corrupted, we still need to dispose opened streams
                    if (ptable != null)
                        ptable.Dispose();

                    // also dispose all previously loaded correct PTables
                    for (int i=0; i<tables.Count; ++i)
                    {
                        for (int j=0; j<tables[i].Count; ++j)
                        {
                            tables[i][j].Dispose();
                        }
                    }

                    throw new CorruptIndexException("Error while loading IndexMap.", exc);
                }
            }
            return tables;
        }

        public void SaveToFile(string filename)
        {
            var tmpIndexMap = string.Format("{0}.{1}.indexmap.tmp", filename, Guid.NewGuid());

            using (var memStream = new MemoryStream())
            using (var memWriter = new StreamWriter(memStream))
            {
                memWriter.WriteLine(new string('0', 32)); // pre-allocate space for MD5 hash
                memWriter.WriteLine(Version);
                memWriter.WriteLine("{0}/{1}", PrepareCheckpoint, CommitCheckpoint);
                for (int i = 0; i < _map.Count; i++)
                {
                    for (int j = 0; j < _map[i].Count; j++)
                    {
                        memWriter.WriteLine("{0},{1},{2}", i, j, new FileInfo(_map[i][j].Filename).Name);
                    }
                }
                memWriter.Flush();

                memStream.Position = 32;
                var hash = MD5Hash.GetHashFor(memStream);

                memStream.Position = 0;
                for (int i = 0; i < hash.Length; ++i)
                {
                    memWriter.Write(hash[i].ToString("X2"));
                }
                memWriter.Flush();

                memStream.Position = 0;
                using (var f = File.OpenWrite(tmpIndexMap)) 
                {
                    f.Write(memStream.GetBuffer(), 0, (int)memStream.Length);
                    f.FlushToDisk();
                }
            }

            int trial = 0;
            while (trial < 5)
            {
                try
                {
                    if (File.Exists(filename))
                        File.Delete(filename);
                    File.Move(tmpIndexMap, filename);
                    break;
                }
                catch (IOException exc)
                {
                    Log.DebugException(exc, "Failed trial to replace indexmap.");
                    trial += 1;
                }
            }
        }

        public MergeResult AddPTable(PTable tableToAdd, 
                                     long prepareCheckpoint, 
                                     long commitCheckpoint, 
                                     Func<IndexEntry, bool> recordExistsAt,
                                     IIndexFilenameProvider filenameProvider)
        {
            Ensure.Nonnegative(prepareCheckpoint, "prepareCheckpoint");
            Ensure.Nonnegative(commitCheckpoint, "commitCheckpoint");

            var tables = CopyFrom(_map);
            CreateIfNeeded(0, tables);
            tables[0].Add(tableToAdd);

            var toDelete = new List<PTable>();
            for (int level = 0; level < tables.Count; level++)
            {
                if (tables[level].Count >= _maxTablesPerLevel)
                {
                    var filename = filenameProvider.GetFilenameNewTable();
                    PTable table = PTable.MergeTo(tables[level], filename, recordExistsAt);
                    CreateIfNeeded(level + 1, tables);
                    tables[level + 1].Add(table);
                    toDelete.AddRange(tables[level]);
                    tables[level].Clear();
                }
            }

            var indexMap = new IndexMap(Version, tables, prepareCheckpoint, commitCheckpoint, _maxTablesPerLevel);
            return new MergeResult(indexMap, toDelete);
        }

        public void Dispose(TimeSpan timeout)
        {
            foreach (var ptable in InOrder())
            {
                ptable.Dispose();
            }
            foreach (var ptable in InOrder())
            {
                ptable.WaitForDisposal(timeout);
            }
        }
    }
}