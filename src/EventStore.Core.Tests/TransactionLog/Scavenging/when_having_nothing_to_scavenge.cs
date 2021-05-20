﻿using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_having_nothing_to_scavenge<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Commit(0, "bla"))
				.Chunk(Rec.Prepare(2, "bla3"),
					Rec.Prepare(2, "bla3"),
					Rec.Commit(1, "bla"),
					Rec.Commit(2, "bla3"))
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
}
