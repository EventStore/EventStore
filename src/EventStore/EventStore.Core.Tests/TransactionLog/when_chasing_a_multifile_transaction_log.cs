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
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.MultifileTransactionFile;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_chasing_a_multifile_transaction_log : SpecificationWithDirectory
    {
        private readonly Guid _correlationId = Guid.NewGuid();
        private readonly Guid _eventId = Guid.NewGuid();

        [Test]
        public void try_read_returns_false_when_writer_checksum_is_zero()
        {
            var writerchk = new InMemoryCheckpoint(0);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new List<ICheckpoint>());
            File.WriteAllBytes(Path.Combine(PathName, "prefix.tf0"), new byte[10000]);
            var reader = new MultifileTransactionFileChaser(config, new InMemoryCheckpoint(0));
            reader.Open();
            LogRecord record;   
            Assert.IsFalse(reader.TryReadNext(out record));
            reader.Close();
        }

        [Test]
        public void try_read_returns_false_when_writer_checksum_is_equal_to_reader_checksum()
        {
            var writerchk = new InMemoryCheckpoint(12);
            var readerchk = new InMemoryCheckpoint("reader", 12);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new[] {readerchk});
            File.WriteAllBytes(Path.Combine(PathName, "prefix.tf0"), new byte[10000]);
            var reader = new MultifileTransactionFileChaser(config, readerchk);
            reader.Open();
            LogRecord record;
            Assert.IsFalse(reader.TryReadNext(out record));
            Assert.AreEqual(12, readerchk.Read());
            
            reader.Close();
        }

        [Test]
        public void try_read_returns_record_when_writerchecksum_ahead()
        {
            var writerchk = new InMemoryCheckpoint(128);
            var readerchk = new InMemoryCheckpoint("reader", 0);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new[] {readerchk});
            var recordToWrite = new PrepareLogRecord(logPosition: 0,
                                                     correlationId: _correlationId,
                                                     eventId: _eventId,
                                                     transactionPosition: 0,
                                                     transactionOffset: 0,
                                                     eventStreamId: "WorldEnding",
                                                     expectedVersion: 1234,
                                                     timeStamp: new DateTime(2012, 12, 21),
                                                     flags: PrepareFlags.None,
                                                     eventType: "type",
                                                     data: new byte[] {1, 2, 3, 4, 5},
                                                     metadata: new byte[] {7, 17});
            using (var fs = new FileStream(Path.Combine(PathName, "prefix.tf0"), FileMode.CreateNew, FileAccess.Write))
            {
                var writer = new BinaryWriter(fs);
                recordToWrite.WriteWithLengthPrefixAndSuffixTo(writer);
                fs.Close();
            }
            var reader = new MultifileTransactionFileChaser(config, readerchk);
            reader.Open();

            LogRecord record;
            var recordRead = reader.TryReadNext(out record);
            reader.Close();
            
            Assert.AreEqual(record.GetSizeWithLengthPrefixAndSuffix(), readerchk.Read());
            Assert.IsTrue(recordRead);
            Assert.AreEqual(recordToWrite, record);
        }


        [Test]
        public void try_read_returns_record_when_record_bigger_than_internal_buffer()
        {
            var writerchk = new InMemoryCheckpoint(9999);
            var readerchk = new InMemoryCheckpoint("reader", 0);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new[] {readerchk});
            var recordToWrite = new PrepareLogRecord(logPosition: 0,
                                                     correlationId: _correlationId,
                                                     eventId: _eventId,
                                                     transactionPosition: 0,
                                                     transactionOffset: 0,
                                                     eventStreamId: "WorldEnding",
                                                     expectedVersion: 1234,
                                                     timeStamp: new DateTime(2012, 12, 21),
                                                     flags: PrepareFlags.None,
                                                     eventType: "type",
                                                     data: new byte[9000],
                                                     metadata: new byte[] {7, 17});
            using (var fs = new FileStream(Path.Combine(PathName, "prefix.tf0"), FileMode.CreateNew, FileAccess.Write))
            {
                var writer = new BinaryWriter(fs);
                recordToWrite.WriteWithLengthPrefixAndSuffixTo(writer);
                fs.Close();
            }
            writerchk.Write(recordToWrite.GetSizeWithLengthPrefixAndSuffix());

            var reader = new MultifileTransactionFileChaser(config, readerchk);
            reader.Open();
            LogRecord record;
            var readRecord = reader.TryReadNext(out record);
            reader.Close();

            Assert.IsTrue(readRecord);
            Assert.AreEqual(record.GetSizeWithLengthPrefixAndSuffix(), readerchk.Read());
            Assert.AreEqual(recordToWrite, record);
        }

        [Test]
        public void try_read_returns_record_when_writerchecksum_equal()
        {
            var writerchk = new InMemoryCheckpoint(0);
            var readerchk = new InMemoryCheckpoint("reader", 0);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk,
                                                     new List<ICheckpoint> {readerchk});
            var recordToWrite = new PrepareLogRecord(logPosition: 0,
                                                     correlationId: _correlationId,
                                                     eventId: _eventId,
                                                     transactionPosition: 0,
                                                     transactionOffset: 0,
                                                     eventStreamId: "WorldEnding",
                                                     expectedVersion: 1234,
                                                     timeStamp: new DateTime(2012, 12, 21),
                                                     flags: PrepareFlags.None,
                                                     eventType: "type",
                                                     data: new byte[] { 1, 2, 3, 4, 5 },
                                                     metadata: new byte[] {7, 17});
            using (var fs = new FileStream(Path.Combine(PathName, "prefix.tf0"), FileMode.CreateNew, FileAccess.Write))
            {
                var writer = new BinaryWriter(fs);
                recordToWrite.WriteWithLengthPrefixAndSuffixTo(writer);
                fs.Close();
            }
            writerchk.Write(recordToWrite.GetSizeWithLengthPrefixAndSuffix());
            
            var reader = new MultifileTransactionFileChaser(config, readerchk);
            reader.Open();
            LogRecord record;
            var readRecord = reader.TryReadNext(out record);
            reader.Close();

            Assert.IsTrue(readRecord);
            Assert.AreEqual(record.GetSizeWithLengthPrefixAndSuffix(), readerchk.Read());
            Assert.AreEqual(recordToWrite, record);
        }

        [Test]
        public void try_read_returns_false_when_writer_checksum_is_ahead_but_not_enough_to_read_record()
        {
            var writerchk = new InMemoryCheckpoint(50);
            var readerchk = new InMemoryCheckpoint("reader", 0);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new[] {readerchk});
            var recordToWrite = new PrepareLogRecord(logPosition: 0,
                                                     correlationId: _correlationId, 
                                                     eventId: _eventId,
                                                     transactionPosition: 0,
                                                     transactionOffset: 0,
                                                     eventStreamId: "WorldEnding",
                                                     expectedVersion: 1234,
                                                     timeStamp: new DateTime(2012, 12, 21),
                                                     flags: PrepareFlags.None,
                                                     eventType: "type",
                                                     data: new byte[] { 1, 2, 3, 4, 5 },
                                                     metadata: new byte[] {7, 17});
            using (var fs = new FileStream(Path.Combine(PathName, "prefix.tf0"), FileMode.CreateNew, FileAccess.Write))
            {
                var writer = new BinaryWriter(fs);
                recordToWrite.WriteWithLengthPrefixAndSuffixTo(writer);
                fs.Close();
            }

            var reader = new MultifileTransactionFileChaser(config, readerchk);
            reader.Open();
            LogRecord record;
            var readRecord = reader.TryReadNext(out record);
            reader.Close();

            Assert.IsFalse(readRecord);
            Assert.AreEqual(0, readerchk.Read());
        }

        [Test]
        public void try_read_returns_false_when_writer_checksum_is_ahead_but_not_enough_to_read_length()
        {
            var writerchk = new InMemoryCheckpoint(3);
            var readerchk = new InMemoryCheckpoint("reader", 0);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk,
                                                     new List<ICheckpoint> {readerchk});
            var recordToWrite = new PrepareLogRecord(logPosition: 0,
                                                     correlationId: _correlationId, 
                                                     eventId: _eventId,
                                                     transactionPosition: 0,
                                                     transactionOffset: 0,
                                                     eventStreamId: "WorldEnding",
                                                     expectedVersion: 1234,
                                                     timeStamp: new DateTime(2012, 12, 21),
                                                     flags: PrepareFlags.None, 
                                                     eventType: "type",
                                                     data: new byte[] { 1, 2, 3, 4, 5 },
                                                     metadata: new byte[] {7, 17});
            using (var fs = new FileStream(Path.Combine(PathName, "prefix.tf0"), FileMode.CreateNew, FileAccess.Write))
            {
                var writer = new BinaryWriter(fs);
                recordToWrite.WriteWithLengthPrefixAndSuffixTo(writer);
                fs.Close();
            }

            var reader = new MultifileTransactionFileChaser(config, readerchk);
            reader.Open();
            LogRecord record;
            var readRecord = reader.TryReadNext(out record);
            reader.Close();
            Assert.IsFalse(readRecord);
            Assert.AreEqual(0, readerchk.Read());
        }

        [Test]
        public void can_read_a_record_straddling_multiple_files()
        {
            var writerchk = new InMemoryCheckpoint(20020);
            var readerchk = new InMemoryCheckpoint("reader", 9990);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new[] {readerchk});
            var recordToWrite = new PrepareLogRecord(logPosition: 0,
                                                     correlationId: _correlationId, 
                                                     eventId: _eventId,
                                                     transactionPosition: 0,
                                                     transactionOffset: 0,
                                                     eventStreamId: "WorldEnding",
                                                     expectedVersion: 1234,
                                                     timeStamp: new DateTime(2012, 12, 21),
                                                     flags: PrepareFlags.None,
                                                     eventType: "type",
                                                     data: new byte[] { 1, 2, 3, 4, 5 },
                                                     metadata: new byte[] {7, 17});
            var memstream = new MemoryStream();
            var writer = new BinaryWriter(memstream);
            recordToWrite.WriteWithLengthPrefixAndSuffixTo(writer);
            var buf = memstream.GetBuffer();
            using (var fs = new FileStream(config.FileNamingStrategy.GetFilenameFor(0), FileMode.CreateNew, FileAccess.Write))
            {
                fs.Seek(9990, SeekOrigin.Begin);
                fs.Write(buf, 0, 10);
                fs.Close();
            }
            using (var fs = new FileStream(config.FileNamingStrategy.GetFilenameFor(1), FileMode.CreateNew, FileAccess.Write))
            {
                fs.Seek(0, SeekOrigin.Begin);
                fs.Write(buf, 10, recordToWrite.GetSizeWithLengthPrefixAndSuffix() - 10);
                fs.Close();
            }

            var reader = new MultifileTransactionFileChaser(config, readerchk);
            reader.Open();
            LogRecord record;
            var readRecord = reader.TryReadNext(out record);
            reader.Close();
            
            Assert.IsTrue(readRecord);
            Assert.That(recordToWrite, Is.EqualTo(record));
            Assert.AreEqual(9990 + recordToWrite.GetSizeWithLengthPrefixAndSuffix(), readerchk.Read());
        }

        [Test]
        public void can_read_a_record_with_length_straddling_multiple_files()
        {
            var writerchk = new InMemoryCheckpoint(20020);
            var readerchk = new InMemoryCheckpoint("reader", 9998);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new[] {readerchk});
            var recordToWrite = new PrepareLogRecord(logPosition: 0,
                                                     correlationId: _correlationId, 
                                                     eventId: _eventId,
                                                     transactionPosition: 0,
                                                     transactionOffset: 0,
                                                     eventStreamId: "WorldEnding",
                                                     expectedVersion: 1234,
                                                     timeStamp: new DateTime(2012, 12, 21),
                                                     flags: PrepareFlags.None,
                                                     eventType: "type",
                                                     data: new byte[] { 1, 2, 3, 4, 5 },
                                                     metadata: new byte[] {7, 17});
            var memstream = new MemoryStream();
            var writer = new BinaryWriter(memstream);
            recordToWrite.WriteWithLengthPrefixAndSuffixTo(writer);
            var buf = memstream.GetBuffer();
            using (var fs = new FileStream(config.FileNamingStrategy.GetFilenameFor(0), FileMode.CreateNew, FileAccess.Write))
            {
                fs.Seek(9998, SeekOrigin.Begin);
                fs.Write(buf, 0, 2);
                fs.Close();
            }
            using (var fs = new FileStream(config.FileNamingStrategy.GetFilenameFor(1), FileMode.CreateNew, FileAccess.Write))
            {
                fs.Seek(0, SeekOrigin.Begin);
                fs.Write(buf, 2, recordToWrite.GetSizeWithLengthPrefixAndSuffix() - 2);
                fs.Close();
            }

            var reader = new MultifileTransactionFileChaser(config, readerchk);
            reader.Open();
            LogRecord record;
            var readRecord = reader.TryReadNext(out record);
            reader.Close();
            
            Assert.IsTrue(readRecord);
            Assert.AreEqual(recordToWrite, record);
            Assert.AreEqual(9998 + recordToWrite.GetSizeWithLengthPrefixAndSuffix(), readerchk.Read());
        }

        [Test]
        public void try_read_returns_properly_when_writer_is_written_to_while_chasing()
        {
            var writerchk = new InMemoryCheckpoint(0);
            var readerchk = new InMemoryCheckpoint("reader", 0);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new[] {readerchk});

            var fileName = Path.Combine(PathName, "prefix.tf0");
            File.Create(fileName).Close();

            var reader = new MultifileTransactionFileChaser(config, readerchk);
            reader.Open();
            
            LogRecord record;
            Assert.IsFalse(reader.TryReadNext(out record));

            var recordToWrite = new PrepareLogRecord(logPosition: 0,
                                                     correlationId: _correlationId, 
                                                     eventId: _eventId,
                                                     transactionPosition: 0,
                                                     transactionOffset: 0,
                                                     eventStreamId: "WorldEnding",
                                                     expectedVersion: 1234,
                                                     timeStamp: new DateTime(2012, 12, 21),
                                                     flags: PrepareFlags.None,
                                                     eventType: "type",
                                                     data: new byte[] { 1, 2, 3, 4, 5 },
                                                     metadata: new byte[] {7, 17});
            var memstream = new MemoryStream();
            var writer = new BinaryWriter(memstream);
            recordToWrite.WriteWithLengthPrefixAndSuffixTo(writer);

            using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                fs.Write(memstream.ToArray(), 0, (int)memstream.Length);
                fs.Flush(flushToDisk: true);
            }
            writerchk.Write(memstream.Length);

            Assert.IsTrue(reader.TryReadNext(out record));
            Assert.AreEqual(record, recordToWrite);

            var recordToWrite2 = new PrepareLogRecord(logPosition: 0,
                                                      correlationId: _correlationId, 
                                                      eventId: _eventId,
                                                      transactionPosition: 0,
                                                      transactionOffset: 0,
                                                      eventStreamId: "WorldEnding",
                                                      expectedVersion: 4321,
                                                      timeStamp: new DateTime(2012, 12, 21),
                                                      flags: PrepareFlags.None,
                                                      eventType: "type",
                                                      data: new byte[] { 3, 2, 1 },
                                                      metadata: new byte[] {9});
            memstream.SetLength(0);
            recordToWrite2.WriteWithLengthPrefixAndSuffixTo(writer);

            using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                fs.Write(memstream.ToArray(), 0, (int) memstream.Length);
                fs.Flush(flushToDisk: true);
            }
            writerchk.Write(writerchk.Read() + memstream.Length);

            Assert.IsTrue(reader.TryReadNext(out record));
            Assert.AreEqual(record, recordToWrite2);

            reader.Close();
        }
    }
}
