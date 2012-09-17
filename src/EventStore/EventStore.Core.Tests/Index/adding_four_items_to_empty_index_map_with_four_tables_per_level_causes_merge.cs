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
using System.IO;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class adding_four_items_to_empty_index_map_with_four_tables_per_level_causes_merge: SpecificationWithDirectoryPerTestFixture
    {
        private string _filename;
        private IndexMap _map;
        private string _mergeFile;
        private MergeResult _result;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _mergeFile = GetTempFilePath();
            _filename = GetTempFilePath();

            _map = IndexMap.FromFile(_filename, x => false, maxTablesPerLevel: 4);
            var memtable = new HashListMemTable();
            memtable.Add(0, 1, 0);

            _result = _map.AddFile(PTable.FromMemtable(memtable, GetTempFilePath()), 1, 2, new GuidFilenameProvider(PathName));
            _result.ToDelete.ForEach(x => x.MarkForDestruction());

            _result = _result.MergedMap.AddFile(PTable.FromMemtable(memtable, GetTempFilePath()), 3, 4, new GuidFilenameProvider(PathName));
            _result.ToDelete.ForEach(x => x.MarkForDestruction());

            _result = _result.MergedMap.AddFile(PTable.FromMemtable(memtable, GetTempFilePath()), 4, 5, new GuidFilenameProvider(PathName));
            _result.ToDelete.ForEach(x => x.MarkForDestruction());

            _result = _result.MergedMap.AddFile(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 1, new FakeFilenameProvider(_mergeFile));
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _result.MergedMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
            File.Delete(_filename);
            File.Delete(_mergeFile);

            base.TestFixtureTearDown();
        }

        [Test]
        public void the_prepare_checkpoint_is_taken_from_the_latest_added_table()
        {
            Assert.AreEqual(0, _result.MergedMap.PrepareCheckpoint);
        }

        [Test]
        public void the_commit_checkpoint_is_taken_from_the_latest_added_table()
        {
            Assert.AreEqual(1, _result.MergedMap.CommitCheckpoint);
        }

        [Test]
        public void there_are_four_items_to_delete()
        {
            Assert.AreEqual(4, _result.ToDelete.Count);
        }

        [Test]
        public void the_merged_map_has_a_single_file()
        {
            Assert.AreEqual(1, _result.MergedMap.GetAllFilenames().Count());
            Assert.AreEqual(_mergeFile, _result.MergedMap.GetAllFilenames().ToList()[0]);
        }

        [Test]
        public void the_original_map_did_not_change()
        {
            Assert.AreEqual(0, _map.InOrder().Count());
            Assert.AreEqual(0, _map.GetAllFilenames().Count());
        }

        [Test]
        public void a_merged_file_was_created()
        {
            Assert.IsTrue(File.Exists(_mergeFile));
        }
    }
}