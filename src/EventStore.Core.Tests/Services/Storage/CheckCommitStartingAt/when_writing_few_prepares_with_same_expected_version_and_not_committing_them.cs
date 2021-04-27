using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.CheckCommitStartingAt {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_writing_few_prepares_with_same_expected_version_and_not_committing_them<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private IPrepareLogRecord _prepare0;
		private IPrepareLogRecord _prepare1;
		private IPrepareLogRecord _prepare2;

		protected override void WriteTestScenario() {
			_prepare0 = WritePrepare("ES", -1);
			_prepare1 = WritePrepare("ES", -1);
			_prepare2 = WritePrepare("ES", -1);
		}

		[Test]
		public void every_prepare_can_be_commited() {
			var res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare0.LogPosition,
				WriterCheckpoint.ReadNonFlushed());

			_streamNameIndex.GetOrAddId("ES", out var streamId);

			Assert.AreEqual(CommitDecision.Ok, res.Decision);
			Assert.AreEqual(streamId, res.EventStreamId);
			Assert.AreEqual(-1, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);

			res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare1.LogPosition, WriterCheckpoint.ReadNonFlushed());

			Assert.AreEqual(CommitDecision.Ok, res.Decision);
			Assert.AreEqual(streamId, res.EventStreamId);
			Assert.AreEqual(-1, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);

			res = ReadIndex.IndexWriter.CheckCommitStartingAt(_prepare2.LogPosition, WriterCheckpoint.ReadNonFlushed());

			Assert.AreEqual(CommitDecision.Ok, res.Decision);
			Assert.AreEqual(streamId, res.EventStreamId);
			Assert.AreEqual(-1, res.CurrentVersion);
			Assert.AreEqual(-1, res.StartEventNumber);
			Assert.AreEqual(-1, res.EndEventNumber);
		}
	}
}
