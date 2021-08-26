using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.CheckCommitStartingAt {
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_writing_single_prepare_log_v3<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private IPrepareLogRecord _prepare;

		protected override void WriteTestScenario() {
			_prepare = WritePrepare("ES", -1);
		}

		[Test]
		public void check_commmit_should_return_idempotent_decision() {
			var res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare.LogPosition,
				WriterCheckpoint.ReadNonFlushed());

			var streamId = _logFormat.StreamIds.LookupValue("ES");

			Assert.AreEqual(CommitDecision.Idempotent, res.Decision);
			Assert.AreEqual(streamId, res.EventStreamId);
			Assert.AreEqual(0, res.CurrentVersion);
			Assert.AreEqual(0, res.StartEventNumber);
			Assert.AreEqual(0, res.EndEventNumber);
		}
	}
}
