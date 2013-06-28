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
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
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
        private readonly Func<IndexEntry, bool> _isHashCollision;
        private readonly int _maxTablesPerLevel;

        private IndexMap(int version,
                         List<List<PTable>> tables, 
                         long prepareCheckpoint,
                         long commitCheckpoint,
                         Func<IndexEntry, bool> isHashCollision, 
                         int maxTablesPerLevel)
        {
            Ensure.Nonnegative(version, "version");
            if (prepareCheckpoint < -1) throw new ArgumentOutOfRangeException("prepareCheckpoint");
            if (commitCheckpoint < -1) throw new ArgumentOutOfRangeException("commitCheckpoint");
            Ensure.NotNull(isHashCollision, "isHashCollision");
            if (maxTablesPerLevel <= 1) throw new ArgumentOutOfRangeException("maxTablesPerLevel");

            Version = version;

            PrepareCheckpoint = prepareCheckpoint;
            CommitCheckpoint = commitCheckpoint;
            
            _map = CopyFrom(tables);
            _isHashCollision = isHashCollision;
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
                throw new CorruptIndexException();
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
            for (int i = 0; i < map.Count; i++)
            {
                // last in the level's list (newest on level) -> to first (oldest on level)
                for (int j = map[i].Count - 1; j >= 0; --j)
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

        public static IndexMap FromFile(string filename, Func<IndexEntry, bool> isHashCollision, int maxTablesPerLevel = 4, bool loadPTables = true)
        {
            var tables = new List<List<PTable>>();
            int version;
            long prepareCheckpoint = -1;
            long commitCheckpoint = -1;

            if (!File.Exists(filename))
                return new IndexMap(IndexMapVersion, tables, prepareCheckpoint, commitCheckpoint, isHashCollision, maxTablesPerLevel);

            using (var f = File.OpenRead(filename))
            using (var reader = new StreamReader(f))
            {
                // calculate real MD5 hash except first 32 bytes which are string representation of stored hash
                f.Position = 32;
                var realHash = MD5Hash.GetHashFor(f);
                f.Position = 0;

                // read stored MD5 hash and convert it from string to byte array
                string text;
                if ((text = reader.ReadLine()) == null)
                    throw new CorruptIndexException("IndexMap file is empty.");
                if (text.Length != 32 || !text.All(x => char.IsDigit(x) || (x >= 'A' && x <= 'F')))
                    throw new CorruptIndexException("Corrupted MD5 hash.");

                // check expected and real hashes are the same
                var expectedHash = new byte[16];
                for (int i = 0; i < 16; ++i)
                {
                    expectedHash[i] = Convert.ToByte(text.Substring(i*2, 2), 16);
                }
                if (expectedHash.Length != realHash.Length)
                    throw new InvalidOperationException("Invalid length of expected and real hash.");
                for (int i = 0; i < realHash.Length; ++i)
                {
                    if (expectedHash[i] != realHash[i])
                        throw new CorruptIndexException("Expected and real hash are different.");
                }

                // at this point we can assume the format is ok, so actually no need to check errors.

                if ((text = reader.ReadLine()) == null)
                    throw new CorruptIndexException("Corrupted version.");
                version = int.Parse(text);

                // read and check prepare/commit checkpoint
                if ((text = reader.ReadLine()) == null)
                    throw new CorruptIndexException("Corrupted commit checkpoint.");

                try
                {
                    var checkpoints = text.Split('/');
                    if (!long.TryParse(checkpoints[0], out prepareCheckpoint) || prepareCheckpoint < -1)
                        throw new CorruptIndexException("Invalid prepare checkpoint.");
                    if (!long.TryParse(checkpoints[1], out commitCheckpoint) || commitCheckpoint < -1)
                         throw new CorruptIndexException("Invalid commit checkpoint.");
                }
                catch(Exception exc)
                {
                    throw new CorruptIndexException("Corrupted prepare/commit checkpoints pair.", exc);
                }

                // all next lines are PTables sorted by levels
                while ((text = reader.ReadLine()) != null)
                {
                    if (prepareCheckpoint < 0 || commitCheckpoint < 0)
                        throw new CorruptIndexException("Negative prepare/commit checkpoint in non-empty IndexMap.");

                    if (!loadPTables)
                        break;

                    PTable ptable = null;
                    var pieces = text.Split(',');
                    try
                    {
                        var level = int.Parse(pieces[0]);
                        var position = int.Parse(pieces[1]);
                        var file = pieces[2];
                        var path = Path.GetDirectoryName(filename);
                        var ptablePath = Path.Combine(path, file);

                        var sw = Stopwatch.StartNew();
                        Log.Trace("Loading PTable '{0}' started...", ptablePath);
                        ptable = PTable.FromFile(ptablePath);
                        Log.Trace("Loading PTable '{0}' done in {1}.", ptablePath, sw.Elapsed);
                        sw.Restart();
                        Log.Trace("Verifying file hash of PTable '{0}' started...", ptablePath);
                        ptable.VerifyFileHash();
                        Log.Trace("Verifying file hash of PTable '{0}' done in {1}.", ptablePath, sw.Elapsed);

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
            }
            return new IndexMap(version, tables, prepareCheckpoint, commitCheckpoint, isHashCollision, maxTablesPerLevel);
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

                using (var f = File.OpenWrite(tmpIndexMap)) 
                using (var fileWriter = new StreamWriter(f))
                {
                    memStream.Position = 0;
                    memStream.CopyTo(f);

                    memStream.Position = 32;
                    var hash = MD5Hash.GetHashFor(memStream);
                    f.Position = 0;
                    for (int i = 0; i < hash.Length; ++i)
                    {
                        fileWriter.Write(hash[i].ToString("X2"));
                    }
                    fileWriter.WriteLine();
                    fileWriter.Flush();
                    f.Flush(flushToDisk: true);
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

        public bool IsCorrupt(string directory)
        {
            return File.Exists(Path.Combine(directory, "merging.m"));
        }

        public void EnterUnsafeState(string directory)
        {
            if (!IsCorrupt(directory))
            {
                using (File.Create(Path.Combine(directory, "merging.m")))
                {
                }
            }
        }

        public void LeaveUnsafeState(string directory)
        {
            File.Delete(Path.Combine(directory, "merging.m"));
        }

        public MergeResult AddFile(PTable tableToAdd, 
                                   long prepareCheckpoint, 
                                   long commitCheckpoint, 
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
                    var table = PTable.MergeTo(tables[level], filename, _isHashCollision);

                    CreateIfNeeded(level + 1, tables);
                    tables[level + 1].Add(table);
                    toDelete.AddRange(tables[level]);
                    tables[level].Clear();
                }
            }

            var indexMap = new IndexMap(Version, tables, prepareCheckpoint, commitCheckpoint, _isHashCollision, _maxTablesPerLevel);
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