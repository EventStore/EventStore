using NUnit.Framework;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Services;
using System;
using EventStore.Core.TransactionLog;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream {
	[TestFixture]
	public class when_hard_deleting_stream_with_log_version_0 : ReadIndexTestScenario {
		protected override void WriteTestScenario() {
			WriteSingleEvent("ES1", 0, new string('.', 3000));
			WriteSingleEvent("ES1", 1, new string('.', 3000));

			WriteV0HardDelete("ES1");
		}

		private void WriteV0HardDelete(string eventStreamId) {
			long pos;
			var logPosition = WriterCheckpoint.ReadNonFlushed();
			var prepare = new PrepareLogRecord(logPosition, Guid.NewGuid(), Guid.NewGuid(), logPosition, 0,
				eventStreamId,
				int.MaxValue - 1, DateTime.UtcNow,
				PrepareFlags.StreamDelete | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
				SystemEventTypes.StreamDeleted, new byte[0], new byte[0],
				prepareRecordVersion: LogRecordVersion.LogRecordV0);
			Writer.Write(prepare, out pos);

			var commit = new CommitLogRecord(WriterCheckpoint.ReadNonFlushed(), prepare.CorrelationId,
				prepare.LogPosition, DateTime.UtcNow, int.MaxValue,
				commitRecordVersion: LogRecordVersion.LogRecordV0);
			Writer.Write(commit, out pos);
		}

		[Test]
		public void should_change_expected_version_to_deleted_event_number_when_reading() {
			var chunk = Db.Manager.GetChunk(0);
			var chunkRecords = new List<LogRecord>();
			RecordReadResult result = chunk.TryReadFirst();
			while (result.Success) {
				chunkRecords.Add(result.LogRecord);
				result = chunk.TryReadClosestForward(result.NextPosition);
			}

			Assert.That(chunkRecords.Any(x =>
				x.RecordType == LogRecordType.Commit && ((CommitLogRecord)x).FirstEventNumber == long.MaxValue));
			Assert.That(chunkRecords.Any(x =>
				x.RecordType == LogRecordType.Prepare && ((PrepareLogRecord)x).ExpectedVersion == long.MaxValue - 1));
		}
	}
}
