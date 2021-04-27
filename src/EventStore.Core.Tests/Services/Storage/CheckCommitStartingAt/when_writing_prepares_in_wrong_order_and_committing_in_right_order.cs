using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.CheckCommitStartingAt {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long), Ignore = "Transactions not supported yet")]
	public class when_writing_prepares_in_wrong_order_and_committing_in_right_order<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private IPrepareLogRecord _prepare0;
		private IPrepareLogRecord _prepare1;
		private IPrepareLogRecord _prepare2;
		private IPrepareLogRecord _prepare3;
		private IPrepareLogRecord _prepare4;

		protected override void WriteTestScenario() {
			_prepare0 = WritePrepare("ES", expectedVersion: -1);
			_prepare1 = WritePrepare("ES", expectedVersion: 2);
			_prepare2 = WritePrepare("ES", expectedVersion: 0);
			_prepare3 = WritePrepare("ES", expectedVersion: 1);
			_prepare4 = WritePrepare("ES", expectedVersion: 3);
			WriteCommit(_prepare0.LogPosition, "ES", eventNumber: 0);
			WriteCommit(_prepare2.LogPosition, "ES", eventNumber: 1);
			WriteCommit(_prepare3.LogPosition, "ES", eventNumber: 2);
		}

		[Test]
		public void check_commmit_on_expected_prepare_should_return_ok_decision() {
			var res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare1.LogPosition,
				WriterCheckpoint.ReadNonFlushed());

			Assert.AreEqual(CommitDecision.Ok, res.Decision);
			Assert.AreEqual("ES", res.EventStreamId);
			Assert.AreEqual(2, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);
		}

		[Test]
		public void check_commmit_on_not_expected_prepare_should_return_wrong_expected_version() {
			var res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare4.LogPosition,
				WriterCheckpoint.ReadNonFlushed());

			Assert.AreEqual(CommitDecision.WrongExpectedVersion, res.Decision);
			Assert.AreEqual("ES", res.EventStreamId);
			Assert.AreEqual(2, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);
		}
	}
}
