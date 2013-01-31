using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.CheckCommitStartingAt
{
    [TestFixture]
    public class when_writing_few_prepares_and_committing_one :ReadIndexTestScenario
    {
        private PrepareLogRecord _prepare0;
        private PrepareLogRecord _prepare1;
        private PrepareLogRecord _prepare2;

        protected override void WriteTestScenario()
        {
            WriteStreamCreated("ES");
            _prepare0 = WritePrepare("ES", expectedVersion: 0);
            _prepare1 = WritePrepare("ES", expectedVersion: 1);
            _prepare2 = WritePrepare("ES", expectedVersion: 2);
            WriteCommit(_prepare0.LogPosition, "ES", eventNumber: 1);
        }

        [Test]
        public void check_commmit_on_2nd_prepare_should_return_ok_decision()
        {
            var res = ReadIndex.CheckCommitStartingAt(_prepare1.LogPosition, WriterCheckpoint.ReadNonFlushed());

            Assert.AreEqual(CommitDecision.Ok, res.Decision);
            Assert.AreEqual("ES", res.EventStreamId);
            Assert.AreEqual(1, res.CurrentVersion);
            Assert.AreEqual(-1, res.StartEventNumber);
            Assert.AreEqual(-1, res.EndEventNumber);
        }

        [Test]
        public void check_commmit_on_3rd_prepare_should_return_wrong_expected_version()
        {
            var res = ReadIndex.CheckCommitStartingAt(_prepare2.LogPosition, WriterCheckpoint.ReadNonFlushed());

            Assert.AreEqual(CommitDecision.WrongExpectedVersion, res.Decision);
            Assert.AreEqual("ES", res.EventStreamId);
            Assert.AreEqual(1, res.CurrentVersion);
            Assert.AreEqual(-1, res.StartEventNumber);
            Assert.AreEqual(-1, res.EndEventNumber);
        }
    }
}
