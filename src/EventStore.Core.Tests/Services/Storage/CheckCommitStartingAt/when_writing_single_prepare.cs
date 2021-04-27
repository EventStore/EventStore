using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.CheckCommitStartingAt {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_writing_single_prepare<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private IPrepareLogRecord _prepare;

		protected override void WriteTestScenario() {
			_prepare = WritePrepare("ES", -1);
		}

		[Test]
		public void check_commmit_should_return_ok_decision() {
			var res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare.LogPosition,
				WriterCheckpoint.ReadNonFlushed());
			_streamNameIndex.GetOrAddId("ES", out var streamId);

			Assert.AreEqual(CommitDecision.Ok, res.Decision);
			Assert.AreEqual(streamId, res.EventStreamId);
			Assert.AreEqual(-1, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);
		}
	}
}
