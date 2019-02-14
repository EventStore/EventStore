using System;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream {
	[TestFixture]
	public class when_reading_deleted_stream_written_with_old_log_record_version : ReadIndexTestScenario {
		private Guid _id1;
		private Guid _id2;
		private Guid _id3;
		private Guid _deleteId;

		protected override void WriteTestScenario() {
			_id1 = Guid.NewGuid();
			_id2 = Guid.NewGuid();
			_id3 = Guid.NewGuid();
			_deleteId = Guid.NewGuid();

			long pos1, pos2, pos3, pos4, pos5, pos6, pos7, pos8;
			Writer.Write(new PrepareLogRecord(0, _id1, _id1, 0, 0, "ES", 0, DateTime.UtcNow,
					PrepareFlags.SingleWrite, "type", new byte[0], new byte[0], LogRecordVersion.LogRecordV0),
				out pos1);
			Writer.Write(new PrepareLogRecord(pos1, _id2, _id2, pos1, 0, "ES", 1, DateTime.UtcNow,
					PrepareFlags.SingleWrite, "type", new byte[0], new byte[0], LogRecordVersion.LogRecordV0),
				out pos2);
			Writer.Write(new PrepareLogRecord(pos2, _id3, _id3, pos2, 0, "ES", 2, DateTime.UtcNow,
					PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]),
				out pos3);
			Writer.Write(new CommitLogRecord(pos3, _id1, 0, DateTime.UtcNow, 0, LogRecordVersion.LogRecordV0),
				out pos4);
			Writer.Write(new CommitLogRecord(pos4, _id2, pos1, DateTime.UtcNow, 1, LogRecordVersion.LogRecordV0),
				out pos5);
			Writer.Write(new CommitLogRecord(pos5, _id3, pos2, DateTime.UtcNow, 2, LogRecordVersion.LogRecordV0),
				out pos6);


			Writer.Write(new PrepareLogRecord(pos6, _deleteId, _deleteId, pos6, 0, "ES", int.MaxValue - 1,
					DateTime.UtcNow,
					PrepareFlags.StreamDelete | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd |
					PrepareFlags.None,
					SystemEventTypes.StreamDeleted, Empty.ByteArray, Empty.ByteArray, LogRecordVersion.LogRecordV0),
				out pos7);
			Writer.Write(
				new CommitLogRecord(pos7, _deleteId, pos6, DateTime.UtcNow, int.MaxValue - 1,
					LogRecordVersion.LogRecordV0), out pos8);
		}

		[Test]
		public void the_stream_is_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("ES"));
		}

		[Test]
		public void the_last_event_number_is_deleted_stream() {
			Assert.AreEqual(EventNumber.DeletedStream, ReadIndex.GetStreamLastEventNumber("ES"));
		}
	}
}
