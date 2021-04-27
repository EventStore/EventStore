using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.CheckCommitStartingAt {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long), Ignore = "Transactions not supported yet")]
	public class when_writing_few_prepares_and_committing_one<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private IPrepareLogRecord _prepare0;
		private IPrepareLogRecord _prepare1;
		private IPrepareLogRecord _prepare2;

		protected override void WriteTestScenario() {
			_prepare0 = WritePrepare("ES", expectedVersion: -1);
			_prepare1 = WritePrepare("ES", expectedVersion: 0);
			_prepare2 = WritePrepare("ES", expectedVersion: 1);
			WriteCommit(_prepare0.LogPosition, "ES", eventNumber: 0);
		}

		[Test]
		public void check_commmit_on_2nd_prepare_should_return_ok_decision() {
			var res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare1.LogPosition,
				WriterCheckpoint.ReadNonFlushed());

			Assert.AreEqual(CommitDecision.Ok, res.Decision);
			Assert.AreEqual("ES", res.EventStreamId);
			Assert.AreEqual(0, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);
		}

		[Test]
		public void check_commmit_on_3rd_prepare_should_return_wrong_expected_version() {
			var res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare2.LogPosition,
				WriterCheckpoint.ReadNonFlushed());

			Assert.AreEqual(CommitDecision.WrongExpectedVersion, res.Decision);
			Assert.AreEqual("ES", res.EventStreamId);
			Assert.AreEqual(0, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);
		}
	}
}
