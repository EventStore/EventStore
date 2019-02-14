using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.CheckCommitStartingAt {
	[TestFixture]
	public class when_writing_single_prepare : ReadIndexTestScenario {
		private PrepareLogRecord _prepare;

		protected override void WriteTestScenario() {
			_prepare = WritePrepare("ES", -1);
		}

		[Test]
		public void check_commmit_should_return_ok_decision() {
			var res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare.LogPosition,
				WriterCheckpoint.ReadNonFlushed());

			Assert.AreEqual(CommitDecision.Ok, res.Decision);
			Assert.AreEqual("ES", res.EventStreamId);
			Assert.AreEqual(-1, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);
		}
	}
}
