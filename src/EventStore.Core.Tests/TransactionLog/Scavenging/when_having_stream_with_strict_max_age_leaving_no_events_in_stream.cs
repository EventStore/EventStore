﻿using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture]
	public class when_having_stream_with_strict_max_age_leaving_no_events_in_stream : ScavengeTestScenario {
		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			return dbCreator
				.Chunk(
					Rec.Prepare(0, "$$bla",
						metadata: new StreamMetadata(null, TimeSpan.FromMinutes(1), null, null, null)),
					Rec.Commit(0, "$$bla"),
					Rec.Prepare(1, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(25)),
					Rec.Commit(1, "bla"),
					Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(20)),
					Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(10)),
					Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(5)),
					Rec.Prepare(2, "bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(3)),
					Rec.Commit(2, "bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				dbResult.Recs[0].Where((x, i) => new[] {0, 1, 7, 8}.Contains(i)).ToArray()
			};
		}

		[Test]
		public void expired_prepares_are_scavenged_but_the_last_in_stream_is_physically_kept() {
			CheckRecords();
		}
	}
}
