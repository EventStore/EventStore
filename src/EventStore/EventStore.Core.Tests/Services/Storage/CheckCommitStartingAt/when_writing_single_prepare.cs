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
    public class when_writing_single_prepare :ReadIndexTestScenario
    {
        private PrepareLogRecord _prepare;

        protected override void WriteTestScenario()
        {
            WriteStreamCreated("ES");
            _prepare = WritePrepare("ES", 0);
        }

        [Test]
        public void check_commmit_should_return_ok_decision()
        {
            var res = ReadIndex.CheckCommitStartingAt(_prepare.LogPosition, WriterChecksum.ReadNonFlushed());

            Assert.AreEqual(CommitDecision.Ok, res.Decision);
            Assert.AreEqual("ES", res.EventStreamId);
            Assert.AreEqual(0, res.CurrentVersion);
            Assert.AreEqual(-1, res.StartEventNumber);
            Assert.AreEqual(-1, res.EndEventNumber);
        }
    }
}
