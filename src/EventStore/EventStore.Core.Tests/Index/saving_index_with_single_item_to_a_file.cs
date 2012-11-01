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
    public class saving_index_with_single_item_to_a_file: SpecificationWithDirectory
    {
        private string _filename;
        private IndexMap _map;
        private string _tablename;
        private string _mergeFile;
        private MergeResult _result;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            _filename = GetFilePathFor("indexfile");
            _tablename = GetTempFilePath();
            _mergeFile = GetFilePathFor("outputfile");

            _map = IndexMap.FromFile(_filename, x => false);
            var memtable = new HashListMemTable(maxSize: 2000);
            memtable.Add(0, 2, 7);
            var table = PTable.FromMemtable(memtable, _tablename);
            _result = _map.AddFile(table, 7, 11, new FakeFilenameProvider(_mergeFile));
            _result.MergedMap.SaveToFile(_filename);
            _result.ToDelete.ForEach(x => x.Dispose());
            _result.MergedMap.InOrder().ToList().ForEach(x => x.Dispose());
            table.Dispose();
        }

        [TearDown]
        public override void TearDown()
        {
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result.MergedMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
            base.TearDown();
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

                Assert.AreEqual(5, lines.Count());
                Assert.AreEqual(md5String, lines[0]);
                Assert.AreEqual(PTable.Version.ToString(), lines[1]);
                Assert.AreEqual("7/11", lines[2]);
                Assert.AreEqual("0,0," + Path.GetFileName(_tablename), lines[3]);
                Assert.AreEqual("", lines[4]);
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