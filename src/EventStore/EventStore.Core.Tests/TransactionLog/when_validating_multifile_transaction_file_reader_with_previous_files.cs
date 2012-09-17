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
using System.Collections.Generic;
using System.IO;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_validating_multifile_transaction_file_reader_with_previous_files : SpecificationWithDirectory
    {
        [Test]
        public void with_file_of_wrong_size_higher_than_checksum_the_file_is_deleted()
        {
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, new InMemoryCheckpoint(500), new List<ICheckpoint>());
            File.WriteAllText(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(0)), "this is just some test blahbydy blah");
            var validator = new TransactionFileDatabaseValidator(config);
            var ex = Assert.Throws<CorruptDatabaseException>(validator.Validate);
            Assert.IsInstanceOf<BadChunkInDatabaseException>(ex.InnerException);
        }

        [Test]
        public void with_not_enough_files_to_reach_checksum_throws()
        {
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, new InMemoryCheckpoint(15000), new List<ICheckpoint>());
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(0)), new byte[10000]);
            var validator = new TransactionFileDatabaseValidator(config);
            Assert.Throws<CorruptDatabaseException>(validator.Validate); 
        }

        [Test]
        public void allows_with_exactly_enough_file_to_reach_checksum()
        {
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, new InMemoryCheckpoint(10000),
                                                     new List<ICheckpoint>());
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(0)), new byte[10000]);
            var validator = new TransactionFileDatabaseValidator(config);
            Assert.DoesNotThrow(validator.Validate);
        }

        [Test]
        public void with_wrong_size_file_less_than_checksum_throws()
        {
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, new InMemoryCheckpoint(15000), new List<ICheckpoint>());
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(0)), new byte[10000]);
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(1)), new byte[9000]);
            var validator = new TransactionFileDatabaseValidator(config);
            var ex = Assert.Throws<CorruptDatabaseException>(validator.Validate);
            Assert.IsInstanceOf<BadChunkInDatabaseException>(ex.InnerException);
        }

        [Test]
        public void when_in_first_extraneous_files_throws_corrupt_database_exception()
        {
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, new InMemoryCheckpoint(9000),
                                                     new List<ICheckpoint>());
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(0)), new byte[10000]);
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(1)), new byte[10000]);
            var validator = new TransactionFileDatabaseValidator(config);
            var ex = Assert.Throws<CorruptDatabaseException>(validator.Validate);
            Assert.IsInstanceOf<ExtraneousFileFoundException>(ex.InnerException);
        }

        [Test]
        public void when_in_multiple_extraneous_files_throws_corrupt_database_exception()
        {
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, new InMemoryCheckpoint(15000),
                                                     new List<ICheckpoint>());
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(0)), new byte[10000]);
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(1)), new byte[10000]);
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(2)), new byte[10000]);
            var validator = new TransactionFileDatabaseValidator(config); 
            var ex = Assert.Throws<CorruptDatabaseException>(validator.Validate);
            Assert.IsInstanceOf<ExtraneousFileFoundException>(ex.InnerException);
        }

        [Test]
        public void when_in_brand_new_extraneous_files_throws_corrupt_database_exception()
        {
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, new InMemoryCheckpoint(0),
                                                     new List<ICheckpoint>());
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(4)), new byte[10000]);
            var validator = new TransactionFileDatabaseValidator(config);
            var ex = Assert.Throws<CorruptDatabaseException>(validator.Validate);
            Assert.IsInstanceOf<ExtraneousFileFoundException>(ex.InnerException);
        }

        [Test]
        public void when_a_reader_checksum_is_ahead_of_writer_checksum_throws_corrupt_database_exception()
        {
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, new InMemoryCheckpoint(0),
                                                     new List<ICheckpoint> {new InMemoryCheckpoint(11)});
            File.WriteAllBytes(Path.Combine(PathName, config.FileNamingStrategy.GetFilenameFor(0)), new byte[10000]);
            var validator = new TransactionFileDatabaseValidator(config);
            var ex = Assert.Throws<CorruptDatabaseException>(validator.Validate);
            Assert.IsInstanceOf<ReaderCheckpointHigherThanWriterException>(ex.InnerException);
        }
    }
}