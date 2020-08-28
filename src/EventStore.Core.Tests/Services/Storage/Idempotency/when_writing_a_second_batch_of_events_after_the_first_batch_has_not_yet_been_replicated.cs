﻿using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Idempotency {
	[TestFixture]
	public class when_writing_a_second_batch_of_events_after_the_first_batch_has_not_yet_been_replicated : WriteEventsToIndexScenario{
		private const int _numEvents = 10;
		private List<Guid> _eventIds = new List<Guid>();
        public override void WriteEvents()
        {
			var expectedEventNumber = -1;
			var transactionPosition = 1000;
			var eventTypes = new List<string>();
			
			for(var i=0;i<_numEvents;i++){
				_eventIds.Add(Guid.NewGuid());
				eventTypes.Add("type");
			}

			var prepares = CreatePrepareLogRecords("stream", expectedEventNumber, eventTypes, _eventIds, transactionPosition);
			var commit = CreateCommitLogRecord(transactionPosition + 1000 * _numEvents, transactionPosition, expectedEventNumber + _numEvents);
			
			/*First batch write: committed to db and pre-committed to index but not yet committed to index*/
			WriteToDB(prepares);
			PreCommitToIndex(prepares);
			
			WriteToDB(commit);
			PreCommitToIndex(commit);
        }

		[Test]
		public void check_commit_with_same_expectedversion_should_return_idempotentnotready_decision() {
			/*Second, idempotent write*/
			var commitCheckResult = _indexWriter.CheckCommit("stream", -1, _eventIds);
			Assert.AreEqual(CommitDecision.IdempotentNotReady, commitCheckResult.Decision);
		}

		[Test]
		public void check_commit_with_expectedversion_any_should_return_idempotentnotready_decision() {
			/*Second, idempotent write*/
			var commitCheckResult = _indexWriter.CheckCommit("stream", ExpectedVersion.Any, _eventIds);
			Assert.AreEqual(CommitDecision.IdempotentNotReady, commitCheckResult.Decision);
		}

		[Test]
		public void check_commit_with_next_expectedversion_should_return_ok_decision() {
			var commitCheckResult = _indexWriter.CheckCommit("stream", _numEvents-1, _eventIds);
			Assert.AreEqual(CommitDecision.Ok, commitCheckResult.Decision);
		}

		[Test]
		public void check_commit_with_incorrect_expectedversion_should_return_wrongexpectedversion_decision() {
			var commitCheckResult = _indexWriter.CheckCommit("stream", _numEvents, _eventIds);
			Assert.AreEqual(CommitDecision.WrongExpectedVersion, commitCheckResult.Decision);
		}

		[Test]
		public void check_commit_with_same_expectedversion_but_different_non_first_event_id_should_return_corruptedidempotency_decision() {
			/*Second, idempotent write but one of the event ids is different*/
			var ids = new List<Guid>();
			foreach(var id in _eventIds)
				ids.Add(id);
			
			ids[ids.Count-2] = Guid.NewGuid();

			var commitCheckResult = _indexWriter.CheckCommit("stream", -1, ids);
			Assert.AreEqual(CommitDecision.CorruptedIdempotency, commitCheckResult.Decision);
		}

		[Test]
		public void check_commit_with_same_expectedversion_but_different_first_event_id_should_return_wrongexpectedversion_decision() {
			/*Second, idempotent write but one of the event ids is different*/
			var ids = new List<Guid>();
			foreach(var id in _eventIds)
				ids.Add(id);
			
			ids[0] = Guid.NewGuid();

			var commitCheckResult = _indexWriter.CheckCommit("stream", -1, ids);
			Assert.AreEqual(CommitDecision.WrongExpectedVersion, commitCheckResult.Decision);
		}
    }
}
