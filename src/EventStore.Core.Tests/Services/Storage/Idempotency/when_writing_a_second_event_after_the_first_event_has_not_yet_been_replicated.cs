using System;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Idempotency {
	[TestFixture]
	public class when_writing_a_second_event_after_the_first_event_has_not_yet_been_replicated : WriteEventsToIndexScenario{
		private Guid _eventId = Guid.NewGuid();
        public override void WriteEvents()
        {
			var expectedEventNumber = -1;
			var transactionPosition = 1000;
			var prepares = CreatePrepareLogRecord("stream", expectedEventNumber, "type", _eventId, transactionPosition);
			var commit = CreateCommitLogRecord(transactionPosition + RecordOffset, transactionPosition, expectedEventNumber + 1);
			
			/*First write: committed to db and pre-committed to index but not yet committed to index*/
			WriteToDB(prepares);
			PreCommitToIndex(prepares);
			
			WriteToDB(commit);
			PreCommitToIndex(commit);
        }

		[Test]
		public void check_commit_with_same_expectedversion_should_return_idempotentnotready_decision() {
			/*Second, idempotent write*/
			var commitCheckResult = _indexWriter.CheckCommit("stream", -1, new Guid[] { _eventId });
			Assert.AreEqual(CommitDecision.IdempotentNotReady, commitCheckResult.Decision);
		}

		[Test]
		public void check_commit_with_expectedversion_any_should_return_idempotentnotready_decision() {
			/*Second, idempotent write*/
			var commitCheckResult = _indexWriter.CheckCommit("stream", ExpectedVersion.Any, new Guid[] { _eventId });
			Assert.AreEqual(CommitDecision.IdempotentNotReady, commitCheckResult.Decision);
		}

		[Test]
		public void check_commit_with_next_expectedversion_should_return_ok_decision() {
			/*Second, idempotent write*/
			var commitCheckResult = _indexWriter.CheckCommit("stream", 0, new Guid[] { _eventId });
			Assert.AreEqual(CommitDecision.Ok, commitCheckResult.Decision);
		}

		[Test]
		public void check_commit_with_incorrect_expectedversion_should_return_wrongexpectedversion_decision() {
			/*Second, idempotent write*/
			var commitCheckResult = _indexWriter.CheckCommit("stream", 1, new Guid[] { _eventId });
			Assert.AreEqual(CommitDecision.WrongExpectedVersion, commitCheckResult.Decision);
		}
    }
}
