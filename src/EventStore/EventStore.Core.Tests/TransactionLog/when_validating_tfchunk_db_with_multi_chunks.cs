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
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_validating_tfchunk_db_with_multi_chunks : SpecificationWithDirectory
    {
        [Test]
        public void with_not_enough_files_to_reach_checksum_throws()
        {
            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             10000,
                                             0,
                                             new InMemoryCheckpoint(25000),
                                             new InMemoryCheckpoint(),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            using (var db = new TFChunkDb(config))
            {
                CreateMultiChunk(config, 0, 1, GetFilePathFor("chunk-000000.000000"));
                Assert.That(() => db.Open(verifyHash: false),
                            Throws.Exception.InstanceOf<CorruptDatabaseException>()
                            .With.InnerException.InstanceOf<ChunkNotFoundException>());
                db.Dispose();
            }
        }

        [Test]
        public void with_checksum_inside_multi_chunk_throws()
        {
            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             10000,
                                             0,
                                             new InMemoryCheckpoint(25000),
                                             new InMemoryCheckpoint(),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            using (var db = new TFChunkDb(config))
            {
                CreateMultiChunk(config, 0, 2, GetFilePathFor("chunk-000000.000000"));
                Assert.That(() => db.Open(verifyHash: false),
                            Throws.Exception.InstanceOf<CorruptDatabaseException>()
                            .With.InnerException.InstanceOf<ChunkNotFoundException>());
            }
        }

        [Test]
        public void allows_with_exactly_enough_file_to_reach_checksum_while_last_is_multi_chunk()
        {
            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             10000,
                                             0,
                                             new InMemoryCheckpoint(30000),
                                             new InMemoryCheckpoint(),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            using (var db = new TFChunkDb(config))
            {
                CreateSingleChunk(config, 0, GetFilePathFor("chunk-000000.000000"));
                CreateMultiChunk(config, 1, 2, GetFilePathFor("chunk-000001.000000"));
                Assert.DoesNotThrow(() => db.Open(verifyHash: false));
            }
        }

        [Test]
        public void allows_next_new_chunk_when_checksum_is_exactly_in_between_two_chunks_if_last_is_ongoing_chunk()
        {
            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             10000,
                                             0,
                                             new InMemoryCheckpoint(20000),
                                             new InMemoryCheckpoint(),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            using (var db = new TFChunkDb(config))
            {
                CreateMultiChunk(config, 0, 1, GetFilePathFor("chunk-000000.000000"));
                CreateOngoingChunk(config, 2, GetFilePathFor("chunk-000002.000000"));
                Assert.DoesNotThrow(() => db.Open(verifyHash: false));
            }
        }

        [Test, Ignore("Due to truncation such situation can happen, so must be considered valid.")]
        public void does_not_allow_next_new_chunk_when_checksum_is_exactly_in_between_two_chunks_and_last_is_multi_chunk()
        {
            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             10000,
                                             0,
                                             new InMemoryCheckpoint(10000),
                                             new InMemoryCheckpoint(),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            using (var db = new TFChunkDb(config))
            {
                CreateSingleChunk(config, 0, GetFilePathFor("chunk-000000.000000"));
                CreateMultiChunk(config, 1, 2, GetFilePathFor("chunk-000001.000000"));

                Assert.That(() => db.Open(verifyHash: false),
                            Throws.Exception.InstanceOf<CorruptDatabaseException>()
                            .With.InnerException.InstanceOf<BadChunkInDatabaseException>());
            }
        }

        [Test]
        public void old_version_of_chunks_are_removed()
        {
            File.Create(GetFilePathFor("foo")).Close();
            File.Create(GetFilePathFor("bla")).Close();

            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             100,
                                             0,
                                             new InMemoryCheckpoint(350),
                                             new InMemoryCheckpoint(),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            using (var db = new TFChunkDb(config))
            {
                CreateSingleChunk(config, 0, GetFilePathFor("chunk-000000.000000"));
                CreateSingleChunk(config, 0, GetFilePathFor("chunk-000000.000002"));
                CreateSingleChunk(config, 0, GetFilePathFor("chunk-000000.000005"));
                CreateSingleChunk(config, 1, GetFilePathFor("chunk-000001.000000"));
                CreateMultiChunk(config, 1, 2, GetFilePathFor("chunk-000001.000001"));
                CreateSingleChunk(config, 2, GetFilePathFor("chunk-000002.000000"));
                CreateSingleChunk(config, 3, GetFilePathFor("chunk-000003.000007"));
                CreateOngoingChunk(config, 3, GetFilePathFor("chunk-000003.000008"));

                Assert.DoesNotThrow(() => db.Open(verifyHash: false));

                Assert.IsTrue(File.Exists(GetFilePathFor("foo")));
                Assert.IsTrue(File.Exists(GetFilePathFor("bla")));
                Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000005")));
                Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000001.000001")));
                Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000003.000008")));
                Assert.AreEqual(5, Directory.GetFiles(PathName, "*").Length);
            }
        }

        [Test]
        public void when_checkpoint_is_exactly_on_the_boundary_of_chunk_the_last_chunk_could_be_not_present_but_should_be_created()
        {
            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             100,
                                             0,
                                             new InMemoryCheckpoint(200),
                                             new InMemoryCheckpoint(),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            using (var db = new TFChunkDb(config))
            {
                CreateMultiChunk(config, 0, 1, GetFilePathFor("chunk-000000.000000"));

                Assert.DoesNotThrow(() => db.Open(verifyHash: false));
                Assert.IsNotNull(db.Manager.GetChunk(2));

                Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000000")));
                Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000002.000000")));
                Assert.AreEqual(2, Directory.GetFiles(PathName, "*").Length);
            }
        }

        [Test]
        public void when_checkpoint_is_exactly_on_the_boundary_of_chunk_the_last_chunk_could_be_present()
        {
            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             100,
                                             0,
                                             new InMemoryCheckpoint(200),
                                             new InMemoryCheckpoint(),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            using (var db = new TFChunkDb(config))
            {
                CreateMultiChunk(config, 0, 1, GetFilePathFor("chunk-000000.000000"));
                CreateOngoingChunk(config, 2, GetFilePathFor("chunk-000002.000001"));

                Assert.DoesNotThrow(() => db.Open(verifyHash: false));
                Assert.IsNotNull(db.Manager.GetChunk(2));

                Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000000")));
                Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000002.000001")));
                Assert.AreEqual(2, Directory.GetFiles(PathName, "*").Length);
            }
        }

        [Test]
        public void when_checkpoint_is_on_boundary_of_new_chunk_and_last_chunk_is_truncated_no_exception_is_thrown()
        {
            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             100,
                                             0,
                                             new InMemoryCheckpoint(300),
                                             new InMemoryCheckpoint(),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            using (var db = new TFChunkDb(config))
            {
                CreateSingleChunk(config, 0, GetFilePathFor("chunk-000000.000000"));
                CreateMultiChunk(config, 1, 2, GetFilePathFor("chunk-000001.000001"), physicalSize: 50, logicalSize: 150);

                Assert.DoesNotThrow(() => db.Open(verifyHash: false));
                Assert.IsNotNull(db.Manager.GetChunk(2));

                Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000000")));
                Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000001.000001")));
                Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000003.000000")));
                Assert.AreEqual(3, Directory.GetFiles(PathName, "*").Length);
            }
        }

        private void CreateSingleChunk(TFChunkDbConfig config, int chunkNum, string filename, int? actualDataSize = null)
        {
            var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, config.ChunkSize, chunkNum, chunkNum, false, Guid.NewGuid());
            var chunkBytes = chunkHeader.AsByteArray();
            var dataSize = actualDataSize ?? config.ChunkSize;
            var buf = new byte[ChunkHeader.Size + dataSize + ChunkFooter.Size];
            Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
            var chunkFooter = new ChunkFooter(true, true, dataSize, dataSize, 0, new byte[ChunkFooter.ChecksumSize]);
            chunkBytes = chunkFooter.AsByteArray();
            Buffer.BlockCopy(chunkBytes, 0, buf, buf.Length - ChunkFooter.Size, chunkBytes.Length);
            File.WriteAllBytes(filename, buf);
        }

        private void CreateMultiChunk(TFChunkDbConfig config, int chunkStartNum, int chunkEndNum, string filename, int? physicalSize = null, long? logicalSize = null)
        {
            if (chunkStartNum > chunkEndNum) throw new ArgumentException("chunkStartNum");

            var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, config.ChunkSize, chunkStartNum, chunkEndNum, false, Guid.NewGuid());
            var chunkBytes = chunkHeader.AsByteArray();
            var physicalDataSize = physicalSize ?? config.ChunkSize;
            var logicalDataSize = logicalSize ?? (chunkEndNum - chunkStartNum + 1) * config.ChunkSize;
            var buf = new byte[ChunkHeader.Size + physicalDataSize + ChunkFooter.Size];
            Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
            var chunkFooter = new ChunkFooter(true, true, physicalDataSize, logicalDataSize, 0, new byte[ChunkFooter.ChecksumSize]);
            chunkBytes = chunkFooter.AsByteArray();
            Buffer.BlockCopy(chunkBytes, 0, buf, buf.Length - ChunkFooter.Size, chunkBytes.Length);
            File.WriteAllBytes(filename, buf);
        }

        private void CreateOngoingChunk(TFChunkDbConfig config, int chunkNum, string filename, int? actualSize = null)
        {
            var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, config.ChunkSize, chunkNum, chunkNum, false, Guid.NewGuid());
            var chunkBytes = chunkHeader.AsByteArray();
            var buf = new byte[ChunkHeader.Size + (actualSize ?? config.ChunkSize) + ChunkFooter.Size];
            Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
            File.WriteAllBytes(filename, buf);
        }
    }
}