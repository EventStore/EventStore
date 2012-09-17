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
using EventStore.Core.Exceptions;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class index_map_should_detect_corruption: SpecificationWithDirectory
    {
        private string _indexMapFileName;
        private string _ptableFileName;
        private PTable _ptable;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            _indexMapFileName = Path.Combine(PathName, "index.map");
            _ptableFileName = Path.Combine(PathName, "ptable");

            var indexMap = IndexMap.FromFile(_indexMapFileName, x => false, maxTablesPerLevel: 2);
            var memtable = new HashListMemTable();
            memtable.Add(0,0,0);
            memtable.Add(1,1,100);
            _ptable = PTable.FromMemtable(memtable, _ptableFileName);

            indexMap = indexMap.AddFile(_ptable, 0, 0, new GuidFilenameProvider(PathName)).MergedMap;
            indexMap.SaveToFile(_indexMapFileName);
        }

        [TearDown]
        public override void TearDown()
        {
            if (_ptable != null)
                _ptable.MarkForDestruction();
            base.TearDown();
        }

        [Test]
        public void when_ptable_file_is_deleted()
        {
            _ptable.MarkForDestruction();
            _ptable = null;
            File.Delete(_ptableFileName);

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_indexmap_file_does_not_have_md5_checksum()
        {
            var lines = File.ReadAllLines(_indexMapFileName);
            File.WriteAllLines(_indexMapFileName, lines.Skip(1));

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_indexmap_file_does_not_have_latest_commit_position()
        {
            var lines = File.ReadAllLines(_indexMapFileName);
            File.WriteAllLines(_indexMapFileName, lines.Where((x,i) => i != 1));

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_indexmap_file_exists_but_is_empty()
        {
            File.WriteAllText(_indexMapFileName, "");

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_indexmap_file_is_garbage()
        {
            File.WriteAllText(_indexMapFileName, "alkfjasd;lkf\nasdfasdf\n");

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_checkpoints_pair_is_corrupted()
        {
            using (var fs = File.Open(_indexMapFileName, FileMode.Open))
            {
                fs.Position = 34;
                var b = (byte)fs.ReadByte();
                b ^= 1;
                fs.Position = 34;
                fs.WriteByte(b);
            }

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_ptable_line_is_missing_one_number()
        {
            var lines = File.ReadAllLines(_indexMapFileName);
            File.WriteAllLines(_indexMapFileName, new[] { lines[0], lines[1], string.Format("0,{0}", _ptableFileName)});

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_ptable_line_constists_only_of_filename()
        {
            var lines = File.ReadAllLines(_indexMapFileName);
            File.WriteAllLines(_indexMapFileName, new[] { lines[0], lines[1], _ptableFileName });

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_ptable_line_is_missing_filename()
        {
            var lines = File.ReadAllLines(_indexMapFileName);
            File.WriteAllLines(_indexMapFileName, new[] { lines[0], lines[1], "0,0" });

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_indexmap_md5_checksum_is_corrupted()
        {
            using (var fs = File.Open(_indexMapFileName, FileMode.Open))
            {
                var b = (byte)fs.ReadByte();
                b ^= 1; // swap single bit
                fs.Position = 0;
                fs.WriteByte(b);
            }

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_ptable_hash_is_corrupted()
        {
            _ptable.Dispose();
            _ptable = null;

            using (var fs = File.Open(_ptableFileName, FileMode.Open))
            {
                fs.Seek(-PTable.MD5Size, SeekOrigin.End);
                var b = (byte)fs.ReadByte();
                b ^= 1;
                fs.Seek(-PTable.MD5Size, SeekOrigin.End);
                fs.WriteByte(b);
            }

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_ptable_type_is_corrupted()
        {
            _ptable.Dispose();
            _ptable = null;

            using (var fs = File.Open(_ptableFileName, FileMode.Open))
            {
                fs.Seek(0, SeekOrigin.Begin);
                fs.WriteByte(123);
            }

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_ptable_header_is_corrupted()
        {
            _ptable.Dispose();
            _ptable = null;

            using (var fs = File.Open(_ptableFileName, FileMode.Open))
            {
                fs.Position = new Random().Next(0, PTableHeader.Size);
                var b = (byte)fs.ReadByte();
                b ^= 1;
                fs.Position -= 1;
                fs.WriteByte(b);
            }

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void when_ptable_data_is_corrupted()
        {
            _ptable.Dispose();
            _ptable = null;

            using (var fs = File.Open(_ptableFileName, FileMode.Open))
            {
                fs.Position = new Random().Next(PTableHeader.Size, (int)fs.Length);
                var b = (byte)fs.ReadByte();
                b ^= 1;
                fs.Position -= 1;
                fs.WriteByte(b);
            }

            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }
    }
}
