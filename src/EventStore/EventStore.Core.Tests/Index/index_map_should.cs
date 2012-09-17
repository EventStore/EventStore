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
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class index_map_should: SpecificationWithDirectory
    {
        private string _indexMapFileName;
        private string _ptableFileName;
        private IndexMap _emptyIndexMap;
        private PTable _ptable;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            _indexMapFileName = Path.Combine(PathName, "index.map");
            _ptableFileName = Path.Combine(PathName, "ptable");

            _emptyIndexMap = IndexMap.FromFile(_indexMapFileName, x => false);

            var memTable = new HashListMemTable();
            memTable.Add(0, 1, 2);
            _ptable = PTable.FromMemtable(memTable, _ptableFileName);
        }

        [TearDown]
        public override void TearDown()
        {
            _ptable.MarkForDestruction();
            base.TearDown();
        }

        [Test]
        public void not_allow_negative_prepare_checkpoint_when_adding_ptable()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _emptyIndexMap.AddFile(_ptable, -1, 0, new GuidFilenameProvider(PathName)));
        }

        [Test]
        public void not_allow_negative_commit_checkpoint_when_adding_ptable()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _emptyIndexMap.AddFile(_ptable, 0, -1, new GuidFilenameProvider(PathName)));
        }

        [Test]
        public void throw_corruptedindexexception_when_prepare_checkpoint_is_less_than_minus_one()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, -2, 0, null);
            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void allow_prepare_checkpoint_equal_to_minus_one_if_no_ptables_are_in_index()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, -1, 0, null);
            Assert.DoesNotThrow(() =>
            {
                var indexMap = IndexMap.FromFile(_indexMapFileName, x => false, 2);
                indexMap.InOrder().ToList().ForEach(x => x.Dispose());
            });
        }

        [Test]
        public void throw_corruptedindexexception_if_prepare_checkpoint_is_minus_one_and_there_are_ptables_in_indexmap()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, -1, 0, _ptableFileName);
            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void throw_corruptedindexexception_when_commit_checkpoint_is_less_than_minus_one()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, 0, -2, null);
            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        [Test]
        public void allow_commit_checkpoint_equal_to_minus_one_if_no_ptables_are_in_index()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, 0, -1, null);
            Assert.DoesNotThrow(() =>
            {
                var indexMap = IndexMap.FromFile(_indexMapFileName, x => false, 2);
                indexMap.InOrder().ToList().ForEach(x => x.Dispose());
            });
        }

        [Test]
        public void throw_corruptedindexexception_if_commit_checkpoint_is_minus_one_and_there_are_ptables_in_indexmap()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, 0, -1, _ptableFileName);
            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, x => false, 2));
        }

        private void CreateArtificialIndexMapFile(string filePath, long prepareCheckpoint, long commitCheckpoint, string ptablePath)
        {
            using (var memStream = new MemoryStream())
            using (var memWriter = new StreamWriter(memStream))
            {
                memWriter.WriteLine(new string('0', 32)); // pre-allocate space for MD5 hash
                memWriter.WriteLine("1");
                memWriter.WriteLine("{0}/{1}", prepareCheckpoint, commitCheckpoint);
                if (!string.IsNullOrWhiteSpace(ptablePath))
                {
                    memWriter.WriteLine("0,0,{0}", ptablePath);
                }
                memWriter.Flush();

                using (var f = File.OpenWrite(filePath))
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
                }
            }
        }
    }
}