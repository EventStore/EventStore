using NUnit.Framework;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Services;
using System;
using EventStore.Core.TransactionLog;
using System.Collections.Generic;
using System.Linq;
using EventStore.LogCommon;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_hard_deleting_stream<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		protected override void WriteTestScenario() {
			WriteSingleEvent("ES1", 0, new string('.', 3000));
			WriteSingleEvent("ES1", 1, new string('.', 3000));
			WriteDelete("ES1");
		}

		[Test]
		public void should_change_expected_version_to_deleted_event_number_when_reading() {
			var chunk = Db.Manager.GetChunk(0);
			var chunkRecords = new List<ILogRecord>();
			RecordReadResult result = chunk.TryReadFirst();
			while (result.Success) {
				chunkRecords.Add(result.LogRecord);
				result = chunk.TryReadClosestForward(result.NextPosition);
			}

			Assert.That(chunkRecords.Any(x =>
				x.RecordType == LogRecordType.Commit && ((CommitLogRecord)x).FirstEventNumber == long.MaxValue));
			Assert.That(chunkRecords.Any(x =>
				x.RecordType == LogRecordType.Prepare && ((IPrepareLogRecord<TStreamId>)x).ExpectedVersion == long.MaxValue - 1));
		}
	}
}
