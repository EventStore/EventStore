//// Copyright (c) 2012, Event Store LLP
//// All rights reserved.
//// 
//// Redistribution and use in source and binary forms, with or without
//// modification, are permitted provided that the following conditions are
//// met:
//// 
//// Redistributions of source code must retain the above copyright notice,
//// this list of conditions and the following disclaimer.
//// Redistributions in binary form must reproduce the above copyright
//// notice, this list of conditions and the following disclaimer in the
//// documentation and/or other materials provided with the distribution.
//// Neither the name of the Event Store LLP nor the names of its
//// contributors may be used to endorse or promote products derived from
//// this software without specific prior written permission
//// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
//// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
//// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
//// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
//// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
//// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
//// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
//// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
//// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
//// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
//// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//// 
//using System;
//using EventStore.Core.TransactionLog;
//using EventStore.Core.TransactionLog.Checkpoint;
//using EventStore.Core.TransactionLog.Chunks;
//using EventStore.Core.TransactionLog.LogRecords;
//using EventStore.Core.TransactionLog.MultifileTransactionFile;
//using NUnit.Framework;

//namespace EventStore.Core.Tests.TransactionLog
//{
//    [TestFixture]
//    public class when_reading_multifile_transaction_log_with_few_records : when_reading_transaction_log_with_few_records
//    {
//        private TransactionFileDatabaseConfig _config;
        
//        protected override void CreateDb(ICheckpoint writerCheckpoint)
//        {
//            _config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerCheckpoint, new ICheckpoint[0]);
//        }

//        protected override ITransactionFileWriter CreateWriter()
//        {
//            return new MultifileTransactionFileWriter(_config);
//        }

//        protected override ITransactionFileReader CreateReader()
//        {
//            return new MultifileTransactionFileReader(_config, _config.WriterCheckpoint);
//        }

//        protected override void DestroyDb()
//        {
//        }
//    }

//    [TestFixture]
//    public class when_reading_chunked_transaction_log_with_few_records : when_reading_transaction_log_with_few_records
//    {
//        private TFChunkDb _db;

//        protected override void CreateDb(ICheckpoint writerCheckpoint)
//        {
//            _db = new TFChunkDb(new TFChunkDbConfig(PathName,
//                                                    new PrefixFileNamingStrategy(PathName, "prefix.tf"),
//                                                    10000,
//                                                    0,
//                                                    writerCheckpoint,
//                                                    new ICheckpoint[0]));
//            _db.OpenVerifyAndClean();
//        }

//        protected override ITransactionFileWriter CreateWriter()
//        {
//            return new TFChunkWriter(_db);
//        }

//        protected override ITransactionFileReader CreateReader()
//        {
//            return new TFChunkReader(_db, _db.Config.WriterCheckpoint);
//        }

//        protected override void DestroyDb()
//        {
//            _db.Dispose();
//        }
//    }

//    [TestFixture]
//    public abstract class when_reading_transaction_log_with_few_records : SpecificationWithDirectory
//    {
//        private ITransactionFileWriter _writer;
//        private ITransactionFileReader _reader;

//        private readonly PrepareLogRecord _prepareRecord;
//        private readonly CommitLogRecord _commitRecord;

//        protected when_reading_transaction_log_with_few_records()
//        {
//            _prepareRecord = new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, "TestStream", 0, DateTime.UtcNow, 
//                                                  PrepareFlags.None, "type", new byte[] {1, 2, 3}, new byte[] {7, 6});
//            _commitRecord = new CommitLogRecord(_prepareRecord.GetSizeWithLengthPrefixAndSuffix(), Guid.NewGuid(), 0, DateTime.UtcNow, 0);
//        }

//        protected abstract void CreateDb(ICheckpoint writerCheckpoint);
//        protected abstract ITransactionFileWriter CreateWriter();
//        protected abstract ITransactionFileReader CreateReader();
//        protected abstract void DestroyDb();

//        public override void SetUp() 
//        {
//            base.SetUp();

//            var writerchk = new InMemoryCheckpoint(0);
//            CreateDb(writerchk);

//            _writer = CreateWriter();
//            _writer.Open();
//            long pos;
//            Assert.IsTrue(_writer.Write(_prepareRecord, out pos));
//            Assert.IsTrue(_writer.Write(_commitRecord, out pos));
//            _writer.Flush();

//            _reader = CreateReader();
//            _reader.Open();
//        }

//        public override void TearDown()
//        {
//            _writer.Close();
//            _reader.Close();
//            DestroyDb();
//            base.TearDown();
//        }

//        [Test]
//        public void reader_sequentially_reads_correct_records_and_returns_correct_positions()
//        {
//            var res1 = _reader.TryReadAt(0);
//            Assert.IsTrue(res1.Success);
//            Assert.AreEqual(_prepareRecord, res1.LogRecord);
//            Assert.AreEqual(_prepareRecord.GetSizeWithLengthPrefixAndSuffix(), res1.NewPosition);

//            var res2 = _reader.TryReadAt(res1.NewPosition);
//            Assert.IsTrue(res2.Success);
//            Assert.AreEqual(_commitRecord, res2.LogRecord);
//            Assert.AreEqual(res1.NewPosition + _commitRecord.GetSizeWithLengthPrefixAndSuffix(), res2.NewPosition);

//            var res3 = _reader.TryReadAt(res2.NewPosition);
//            Assert.IsFalse(res3.Success);
//        }

//        [Test]
//        public void reader_correctly_seeks_to_the_beginning_after_all_records_read()
//        {
//            var res1 = _reader.TryReadAt(0);
//            Assert.IsTrue(res1.Success);
//            Assert.AreEqual(_prepareRecord, res1.LogRecord);
//            Assert.AreEqual(_prepareRecord.GetSizeWithLengthPrefixAndSuffix(), res1.NewPosition);

//            var res2 = _reader.TryReadAt(res1.NewPosition);
//            Assert.IsTrue(res2.Success);
//            Assert.AreEqual(_commitRecord, res2.LogRecord);
//            //Assert.AreEqual(res1.NewPosition + _commitRecord.GetSizeWithLengthPrefixAndSuffix(), res2.NewPosition);

//            var res3 = _reader.TryReadAt(0);
//            Assert.IsTrue(res3.Success);
//            Assert.AreEqual(_prepareRecord, res3.LogRecord);
//            //Assert.AreEqual(_prepareRecord.GetSizeWithLengthPrefixAndSuffix(), res3.NextPosition);
//        }

//        [Test]
//        public void reader_is_able_to_read_records_from_the_middle_of_db()
//        {
//            var res1 = _reader.TryReadAt(_prepareRecord.GetSizeWithLengthPrefixAndSuffix());
//            Assert.IsTrue(res1.Success);
//            Assert.AreEqual(_commitRecord, res1.LogRecord);
//            //Assert.AreEqual(_prepareRecord.GetSizeWithLengthPrefixAndSuffix() + _commitRecord.GetSizeWithLengthPrefixAndSuffix(), 
//            //                res1.NextPosition);
//        }

//        [Test]
//        public void try_read_returns_record_once_it_is_written_later_when_previous_try_failed()
//        {
//            var p1 = _prepareRecord.GetSizeWithLengthPrefixAndSuffix();
//            var p2 = p1 + _commitRecord.GetSizeWithLengthPrefixAndSuffix();

//            Assert.IsTrue(_reader.TryReadAt(0).Success);
//            Assert.IsTrue(_reader.TryReadAt(p1).Success);
            
//            Assert.IsFalse(_reader.TryReadAt(p2).Success);

//            var rec = LogRecord.SingleWrite(0, Guid.NewGuid(), Guid.NewGuid(), "ES", -1, "ET", new byte[] { 7 }, null);
//            long pos;
//            Assert.IsTrue(_writer.Write(rec, out pos));
//            _writer.Flush();

//            var res = _reader.TryReadAt(p2);
//            Assert.IsTrue(res.Success);
//            Assert.AreEqual(rec, res.LogRecord);
//            //Assert.AreEqual(p2 + rec.GetSizeWithLengthPrefixAndSuffix(), res.NextPosition);
//        }

//    }
//}