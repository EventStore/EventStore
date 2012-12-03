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
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class saving_index_with_six_items_to_a_file: SpecificationWithDirectory
    {
        private string _filename;
        private string _tablename;
        private string _mergeFile;
        private IndexMap _map;
        private MergeResult _result;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            _filename = GetFilePathFor("indexfile");
            _tablename = GetTempFilePath();
            _mergeFile = GetFilePathFor("outfile");

            _map = IndexMap.FromFile(_filename, x => false, maxTablesPerLevel: 4);
            var memtable = new HashListMemTable(maxSize: 10);
            memtable.Add(0, 2, 123);
            var table = PTable.FromMemtable(memtable, _tablename);
            _result = _map.AddFile(table, 0, 0, new FakeFilenameProvider(_mergeFile));
            _result = _result.MergedMap.AddFile(table, 0, 0, new FakeFilenameProvider(_mergeFile));
            _result = _result.MergedMap.AddFile(table, 0, 0, new FakeFilenameProvider(_mergeFile));
            var merged = _result.MergedMap.AddFile(table, 0, 0, new FakeFilenameProvider(_mergeFile));
            _result = merged.MergedMap.AddFile(table, 0, 0, new FakeFilenameProvider(_mergeFile));
            _result = _result.MergedMap.AddFile(table, 7, 11, new FakeFilenameProvider(_mergeFile));
            _result.MergedMap.SaveToFile(_filename);

            table.Dispose();
        
            merged.MergedMap.InOrder().ToList().ForEach(x => x.Dispose());
            merged.ToDelete.ForEach(x => x.Dispose());

            _result.MergedMap.InOrder().ToList().ForEach(x => x.Dispose());
            _result.ToDelete.ForEach(x => x.Dispose());
        }

        [Test]
        public void the_file_exists()
        {
            Assert.IsTrue(File.Exists(_filename));
        }

        [Test]
        public void the_file_contains_correct_data()
        {
            using (var fs = File.OpenRead(_filename))
            using (var reader = new StreamReader(fs))
            {
                var text = reader.ReadToEnd();
                var lines = text.Replace("\r", "").Split('\n');

                fs.Position = 32;
                var md5 = MD5Hash.GetHashFor(fs);
                var md5String = BitConverter.ToString(md5).Replace("-", "");

                Assert.AreEqual(7, lines.Count());
                Assert.AreEqual(md5String, lines[0]);
                Assert.AreEqual(PTable.Version.ToString(), lines[1]);
                Assert.AreEqual("7/11", lines[2]);
                var name = new FileInfo(_tablename).Name;
                Assert.AreEqual("0,0," + name, lines[3]);
                Assert.AreEqual("0,1," + name, lines[4]);
                Assert.AreEqual("1,0," + Path.GetFileName(_mergeFile), lines[5]);
                Assert.AreEqual("", lines[6]);
            }
        }

        [Test]
        public void saved_file_could_be_read_correctly_and_without_errors()
        {
            var map = IndexMap.FromFile(_filename, x => false);
            map.InOrder().ToList().ForEach(x => x.Dispose());

            Assert.AreEqual(7, map.PrepareCheckpoint);
            Assert.AreEqual(11, map.CommitCheckpoint);
        }
    }
}