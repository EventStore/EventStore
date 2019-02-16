using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.CheckCommitStartingAt {
	[TestFixture]
	public class when_writing_few_prepares_with_same_expected_version_and_not_committing_them : ReadIndexTestScenario {
		private PrepareLogRecord _prepare0;
		private PrepareLogRecord _prepare1;
		private PrepareLogRecord _prepare2;

		protected override void WriteTestScenario() {
			_prepare0 = WritePrepare("ES", -1);
			_prepare1 = WritePrepare("ES", -1);
			_prepare2 = WritePrepare("ES", -1);
		}

		[Test]
		public void every_prepare_can_be_commited() {
			var res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare0.LogPosition,
				WriterCheckpoint.ReadNonFlushed());

			Assert.AreEqual(CommitDecision.Ok, res.Decision);
			Assert.AreEqual("ES", res.EventStreamId);
			Assert.AreEqual(-1, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);

			res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare1.LogPosition, WriterCheckpoint.ReadNonFlushed());

			Assert.AreEqual(CommitDecision.Ok, res.Decision);
			Assert.AreEqual("ES", res.EventStreamId);
			Assert.AreEqual(-1, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);

			res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare2.LogPosition, WriterCheckpoint.ReadNonFlushed());

			Assert.AreEqual(CommitDecision.Ok, res.Decision);
			Assert.AreEqual("ES", res.EventStreamId);
			Assert.AreEqual(-1, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);
		}
	}
}
