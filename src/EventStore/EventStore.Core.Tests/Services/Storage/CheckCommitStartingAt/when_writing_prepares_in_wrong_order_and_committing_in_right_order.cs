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
    public class when_writing_prepares_in_wrong_order_and_committing_in_right_order :ReadIndexTestScenario
    {
        private PrepareLogRecord _prepare0;
        private PrepareLogRecord _prepare1;
        private PrepareLogRecord _prepare2;
        private PrepareLogRecord _prepare3;
        private PrepareLogRecord _prepare4;

        protected override void WriteTestScenario()
        {
            WriteStreamCreated("ES");
            _prepare0 = WritePrepare("ES", expectedVersion: 0);
            _prepare1 = WritePrepare("ES", expectedVersion: 3);
            _prepare2 = WritePrepare("ES", expectedVersion: 1);
            _prepare3 = WritePrepare("ES", expectedVersion: 2);
            _prepare4 = WritePrepare("ES", expectedVersion: 4);
            WriteCommit(_prepare0.LogPosition, "ES", eventNumber: 1);
            WriteCommit(_prepare2.LogPosition, "ES", eventNumber: 2);
            WriteCommit(_prepare3.LogPosition, "ES", eventNumber: 3);
        }

        [Test]
        public void check_commmit_on_expected_prepare_should_return_ok_decision()
        {
            var res = ReadIndex.CheckCommitStartingAt(_prepare1.LogPosition, WriterCheckpoint.ReadNonFlushed());

            Assert.AreEqual(CommitDecision.Ok, res.Decision);
            Assert.AreEqual("ES", res.EventStreamId);
            Assert.AreEqual(3, res.CurrentVersion);
            Assert.AreEqual(-1, res.StartEventNumber);
            Assert.AreEqual(-1, res.EndEventNumber);
        }

        [Test]
        public void check_commmit_on_not_expected_prepare_should_return_wrong_expected_version()
        {
            var res = ReadIndex.CheckCommitStartingAt(_prepare4.LogPosition, WriterCheckpoint.ReadNonFlushed());

            Assert.AreEqual(CommitDecision.WrongExpectedVersion, res.Decision);
            Assert.AreEqual("ES", res.EventStreamId);
            Assert.AreEqual(3, res.CurrentVersion);
            Assert.AreEqual(-1, res.StartEventNumber);
            Assert.AreEqual(-1, res.EndEventNumber);
        }
    }
}
