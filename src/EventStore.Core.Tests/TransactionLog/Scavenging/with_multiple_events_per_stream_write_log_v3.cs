using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	public class with_multiple_events_per_stream_write_log_v3<TLogFormat, TStreamId> {
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_stream_is_deleted : ScavengeTestScenario<TLogFormat, TStreamId>{
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(Rec.Prepare(0, "bla", numEvents: 3),
						Rec.Prepare(1, "bla", numEvents: 3),
						Rec.Prepare(2, "bla", numEvents: 3),
						Rec.Delete(3, "bla"))
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep = new int[] { 0, 4 };

				return new[] {
					dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToArray(),
				};
			}

			[Test]
			public void only_delete_tombstone_records_with_their_commits_are_kept() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_having_nothing_to_scavenge : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(Rec.Prepare(0, "bla", numEvents: 3),
						Rec.Prepare(1, "bla"))
					.Chunk(Rec.Prepare(2, "bla3", numEvents: 2),
						Rec.Prepare(3, "bla3"))
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				return dbResult.Recs;
			}

			[Test]
			public void all_records_are_kept_untouched() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_having_stream_with_max_age : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(
						Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(null, TimeSpan.FromMinutes(5), null, null, null)),
						Rec.Prepare(1, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(20), numEvents: 2),
						Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(12), numEvents: 2),
						Rec.Prepare(3, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(10), numEvents: 3),
						Rec.Prepare(4, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(5), numEvents: 4),
						Rec.Prepare(6, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(2), numEvents: 2),
						Rec.Prepare(7, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(1), numEvents: 2))
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep =  new int[] { 0, 1, 6, 7 };

				return new[] {
					dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToArray(),
				};
			}

			[Test]
			public void expired_prepares_are_scavenged() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_having_stream_with_max_age_and_all_expired : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(
						Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(null, TimeSpan.FromMinutes(5), null, null, null)),
						Rec.Prepare(1, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(20), numEvents: 2), // events 0-1
						Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(12), numEvents: 2), // events 2-3
						Rec.Prepare(3, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(10), numEvents: 3), // events 4-6
						Rec.Prepare(4, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(9), numEvents: 4), // events 7-10
						Rec.Prepare(6, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(7), numEvents: 2), // events 11-12
						Rec.Prepare(7, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(6), numEvents: 2)) // events 13-14
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep =  new int[] { 0, 1, 7 };

				var records = dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToList();
				var modifiedPrepare = (IPrepareLogRecord<TStreamId>) records.Last();
				records[^1] = LogRecord.Prepare(
					LogFormatHelper<TLogFormat,TStreamId>.RecordFactory,
					modifiedPrepare.Events[1].EventLogPosition!.Value,
					modifiedPrepare.CorrelationId,
					modifiedPrepare.TransactionPosition,
					modifiedPrepare.TransactionOffset,
					modifiedPrepare.EventStreamId,
					14 - 1,
					modifiedPrepare.Flags,
					modifiedPrepare.Events.Skip(1).ToArray(),
					modifiedPrepare.TimeStamp
					);
				return new[] {records.ToArray()};
			}

			[Test]
			public void expired_prepares_are_scavenged() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_having_stream_with_max_count : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(
						Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(4, null, null, null, null)),
						Rec.Prepare(1, "bla", numEvents: 2), //events 0-1
						Rec.Prepare(2, "bla", numEvents: 2), //events 2-3
						Rec.Prepare(3, "bla", numEvents: 3), //events 4-6
						Rec.Prepare(4, "bla", numEvents: 4), //events 7-10
						Rec.Prepare(6, "bla", numEvents: 4), //events 11-14
						Rec.Prepare(7, "bla", numEvents: 2)) //events 15-16
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep =  new int[] { 0, 1, 6, 7 };

				var records = dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToList();
				var modifiedPrepare = (IPrepareLogRecord<TStreamId>) records[^2];
				var logPosOffset = modifiedPrepare.Events[0].EventLogPosition!.Value - modifiedPrepare.LogPosition;
				records[^2] = LogRecord.Prepare(
					LogFormatHelper<TLogFormat,TStreamId>.RecordFactory,
					modifiedPrepare.Events[2].EventLogPosition!.Value - logPosOffset,
					modifiedPrepare.CorrelationId,
					modifiedPrepare.TransactionPosition,
					modifiedPrepare.TransactionOffset,
					modifiedPrepare.EventStreamId,
					13 - 1,
					modifiedPrepare.Flags,
					modifiedPrepare.Events.Skip(2).ToArray(),
					modifiedPrepare.TimeStamp
				);
				return new[] {records.ToArray()};
			}

			[Test]
			public void expired_prepares_are_scavenged() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_having_stream_with_max_count_at_prepare_events_boundary : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(
						Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(6, null, null, null, null)),
						Rec.Prepare(1, "bla", numEvents: 2), //events 0-1
						Rec.Prepare(2, "bla", numEvents: 2), //events 2-3
						Rec.Prepare(3, "bla", numEvents: 3), //events 4-6
						Rec.Prepare(4, "bla", numEvents: 4), //events 7-10
						Rec.Prepare(6, "bla", numEvents: 4), //events 11-14
						Rec.Prepare(7, "bla", numEvents: 2)) //events 15-16
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep =  new int[] { 0, 1, 6, 7 };

				return new[] {
					dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToArray(),
				};
			}

			[Test]
			public void expired_prepares_are_scavenged_and_kept_records_are_intact() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_having_stream_with_both_max_age_and_max_count_and_stricter_max_age : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(
						Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(3, TimeSpan.FromMinutes(5), null, null, null)),
						Rec.Prepare(1, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(20), numEvents: 2),
						Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(12), numEvents: 2),
						Rec.Prepare(3, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(10), numEvents: 3),
						Rec.Prepare(4, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(7), numEvents: 4),
						Rec.Prepare(6, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(6), numEvents: 2),
						Rec.Prepare(7, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(4), numEvents: 2))
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep =  new int[] { 0, 1, 7 };

				return new[] {
					dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToArray(),
				};
			}

			[Test]
			public void expired_prepares_are_scavenged() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_having_stream_with_both_max_age_and_max_count_and_stricter_max_count : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(
						Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(6, TimeSpan.FromMinutes(5), null, null, null)),
						Rec.Prepare(1, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(20), numEvents: 2), //events 0-1
						Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(12), numEvents: 2), //events 2-3
						Rec.Prepare(3, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(4), numEvents: 3), //events 4-6
						Rec.Prepare(4, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(3), numEvents: 4), //events 7-10
						Rec.Prepare(6, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(2), numEvents: 2), //events 11-12
						Rec.Prepare(7, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(1), numEvents: 2)) //events 13-14
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep =  new int[] { 0, 1, 5, 6, 7 };

				var records = dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToArray();
				var modifiedPrepare = (IPrepareLogRecord<TStreamId>) records[^3];
				var logPosOffset = modifiedPrepare.Events[0].EventLogPosition!.Value - modifiedPrepare.LogPosition;
				records[^3] = LogRecord.Prepare(
					LogFormatHelper<TLogFormat,TStreamId>.RecordFactory,
					modifiedPrepare.Events[2].EventLogPosition!.Value - logPosOffset,
					modifiedPrepare.CorrelationId,
					modifiedPrepare.TransactionPosition,
					modifiedPrepare.TransactionOffset,
					modifiedPrepare.EventStreamId,
					9 - 1,
					modifiedPrepare.Flags,
					modifiedPrepare.Events.Skip(2).ToArray(),
					modifiedPrepare.TimeStamp
				);
				return new[] {records.ToArray()};
			}

			[Test]
			public void expired_prepares_are_scavenged() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_stream_having_metadata_is_deleted : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(10, null, null, null, null), numEvents: 3),
						Rec.Prepare(1, "$$bla", metadata: new StreamMetadata(2, null, null, null, null), numEvents: 3),
						Rec.Delete(2, "bla"))
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				return new[] { dbResult.Recs[0].Where((x, i) => i == 0 || i >= 3).ToArray() };
			}

			[Test]
			public void meta_stream_is_scavenged_as_well() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_stream_is_soft_deleted : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(truncateBefore: EventNumber.DeletedStream)),
						Rec.Prepare(1, "bla", numEvents: 2), //events 0-1
						Rec.Prepare(2, "bla", numEvents: 3), //events 2-4
						Rec.Prepare(3, "bla", numEvents: 4), //events 5-8
						Rec.Prepare(4, "bla", numEvents: 3), //events 9-11
						Rec.Prepare(5, "bla", numEvents: 5)) //events 12-16
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep = new int[] { 0, 1, 6 };

				var records = dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToList();
				var modifiedPrepare = (IPrepareLogRecord<TStreamId>) records[^1];
				records[^1] = LogRecord.Prepare(
					LogFormatHelper<TLogFormat,TStreamId>.RecordFactory,
					modifiedPrepare.Events[4].EventLogPosition!.Value,
					modifiedPrepare.CorrelationId,
					modifiedPrepare.TransactionPosition,
					modifiedPrepare.TransactionOffset,
					modifiedPrepare.EventStreamId,
					16 - 1,
					modifiedPrepare.Flags,
					modifiedPrepare.Events.Skip(4).ToArray(),
					modifiedPrepare.TimeStamp
				);
				return new[] {records.ToArray()};
			}

			[Test]
			public void last_prepare_should_be_kept() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_truncate_before_is_specified : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(truncateBefore: 6)),
						Rec.Prepare(1, "bla", numEvents: 2), //events 0-1
						Rec.Prepare(2, "bla", numEvents: 3), //events 2-4
						Rec.Prepare(3, "bla", numEvents: 4), //events 5-8
						Rec.Prepare(4, "bla", numEvents: 3), //events 9-11
						Rec.Prepare(5, "bla", numEvents: 2)) //events 12-13
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep = new int[] { 0, 1, 4, 5, 6 };

				var records = dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToArray();
				var modifiedPrepare = (IPrepareLogRecord<TStreamId>) records[^3];
				var logPosOffset = modifiedPrepare.Events[0].EventLogPosition!.Value - modifiedPrepare.LogPosition;
				records[^3] = LogRecord.Prepare(
					LogFormatHelper<TLogFormat,TStreamId>.RecordFactory,
					modifiedPrepare.Events[1].EventLogPosition!.Value - logPosOffset,
					modifiedPrepare.CorrelationId,
					modifiedPrepare.TransactionPosition,
					modifiedPrepare.TransactionOffset,
					modifiedPrepare.EventStreamId,
					6 - 1,
					modifiedPrepare.Flags,
					modifiedPrepare.Events.Skip(1).ToArray(),
					modifiedPrepare.TimeStamp
				);
				return new[] {records.ToArray()};
			}

			[Test]
			public void truncated_records_are_scavenged() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_truncate_before_is_specified_at_prepare_events_boundary : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(truncateBefore: 9)),
						Rec.Prepare(1, "bla", numEvents: 2), //events 0-1
						Rec.Prepare(2, "bla", numEvents: 3), //events 2-4
						Rec.Prepare(3, "bla", numEvents: 4), //events 5-8
						Rec.Prepare(4, "bla", numEvents: 3), //events 9-11
						Rec.Prepare(5, "bla", numEvents: 2)) //events 12-13
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep = new int[] { 0, 1, 5, 6 };

				return new[] {
					dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToArray(),
				};
			}

			[Test]
			public void truncated_records_are_scavenged_and_kept_records_are_intact() {
				CheckRecords();
			}
		}

		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_exactly_one_event_is_remaining_in_partial_log_record : ScavengeTestScenario<TLogFormat, TStreamId> {
			protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
				return dbCreator
					.Chunk(
						Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(truncateBefore: 12)),
						Rec.Prepare(7, "bla", numEvents: 13))
					.CompleteLastChunk()
					.CreateDb();
			}

			protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
				var keep =  new int[] { 0, 1, 2 };

				var records = dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToList();
				var modifiedPrepare = (IPrepareLogRecord<TStreamId>) records.Last();
				records[^1] = LogRecord.Prepare(
					LogFormatHelper<TLogFormat,TStreamId>.RecordFactory,
					//when there is exactly one event, we don't offset the log position
					//and use the event's log position as the prepare's log position
					modifiedPrepare.Events[^1].EventLogPosition!.Value,
					modifiedPrepare.CorrelationId,
					modifiedPrepare.TransactionPosition,
					modifiedPrepare.TransactionOffset,
					modifiedPrepare.EventStreamId,
					12 - 1,
					modifiedPrepare.Flags,
					modifiedPrepare.Events.Skip(12).ToArray(),
					modifiedPrepare.TimeStamp
				);
				return new[] {records.ToArray()};
			}

			[Test]
			public void expired_prepares_are_scavenged() {
				CheckRecords();
			}
		}
	}
}
