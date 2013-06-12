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
using System.IO;
using EventStore.Core.Tests.TransactionLog.Validation;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Truncation
{
    [TestFixture]
    public class when_truncating_right_at_the_end_of_multichunk : SpecificationWithDirectoryPerTestFixture
    {
        private TFChunkDbConfig _config;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _config = new TFChunkDbConfig(PathName,
                                          new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                          1000,
                                          0,
                                          new InMemoryCheckpoint(13500),
                                          new InMemoryCheckpoint(5500),
                                          new InMemoryCheckpoint(5500),
                                          new InMemoryCheckpoint(11000));

            DbUtil.CreateMultiChunk(_config, 0, 2, GetFilePathFor("chunk-000000.000001"));
            DbUtil.CreateMultiChunk(_config, 0, 2, GetFilePathFor("chunk-000000.000002"));
            DbUtil.CreateMultiChunk(_config, 3, 10, GetFilePathFor("chunk-000003.000001"));
            DbUtil.CreateMultiChunk(_config, 3, 10, GetFilePathFor("chunk-000003.000002"));
            DbUtil.CreateMultiChunk(_config, 7, 8, GetFilePathFor("chunk-000007.000001"));
            DbUtil.CreateMultiChunk(_config, 11, 12, GetFilePathFor("chunk-000011.000001"));
            DbUtil.CreateOngoingChunk(_config, 13, GetFilePathFor("chunk-000013.000000"));

            var truncator = new TFChunkDbTruncator(_config);
            truncator.TruncateDb(_config.TruncateCheckpoint.ReadNonFlushed());
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            using (var db = new TFChunkDb(_config))
            {
                Assert.DoesNotThrow(() => db.Open(verifyHash: false));
            }
            Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000002")));
            Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000003.000002")));
            Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000011.000000")));
            Assert.AreEqual(3, Directory.GetFiles(PathName, "*").Length);

            base.TestFixtureTearDown();
        }

        [Test]
        public void writer_checkpoint_should_be_set_to_truncate_checkpoint()
        {
            Assert.AreEqual(11000, _config.WriterCheckpoint.Read());
            Assert.AreEqual(11000, _config.WriterCheckpoint.ReadNonFlushed());
        }

        [Test]
        public void chaser_checkpoint_should_be_left_as_is_if_less_than_truncate_checkpoint()
        {
            Assert.AreEqual(5500, _config.ChaserCheckpoint.Read());
            Assert.AreEqual(5500, _config.ChaserCheckpoint.ReadNonFlushed());
        }

        [Test]
        public void epoch_checkpoint_should_be_left_as_is_if_less_than_truncate_checkpoint()
        {
            Assert.AreEqual(5500, _config.EpochCheckpoint.Read());
            Assert.AreEqual(5500, _config.EpochCheckpoint.ReadNonFlushed());
        }

        [Test]
        public void truncate_checkpoint_should_be_reset_after_truncation()
        {
            Assert.AreEqual(-1, _config.TruncateCheckpoint.Read());
            Assert.AreEqual(-1, _config.TruncateCheckpoint.ReadNonFlushed());
        }

        [Test]
        public void every_chunk_that_end_by_the_truncation_checkpoint_is_preserved()
        {
            Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000001")));
            Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000002")));
            Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000003.000001")));
            Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000003.000002")));
            Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000007.000001")));
            Assert.AreEqual(5, Directory.GetFiles(PathName, "*").Length);
        }
    }
}