﻿using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture]
	public class when_having_stream_with_truncatebefore_specified : ScavengeTestScenario {
		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(truncateBefore: 7)),
					Rec.Commit(0, "$$bla"),
					Rec.Prepare(1, "bla"), // event 0
					Rec.Commit(1, "bla"),
					Rec.Prepare(2, "bla"), // event 1
					Rec.Prepare(2, "bla"), // event 2
					Rec.Prepare(2, "bla"), // event 3
					Rec.Prepare(2, "bla"), // event 4
					Rec.Prepare(2, "bla"), // event 5
					Rec.Commit(2, "bla"),
					Rec.Prepare(3, "bla"), // event 6
					Rec.Prepare(3, "bla"), // event 7
					Rec.Prepare(3, "bla"), // event 8
					Rec.Prepare(3, "bla"), // event 9
					Rec.Commit(3, "bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				dbResult.Recs[0].Where((x, i) => new[] {0, 1, 11, 12, 13, 14}.Contains(i)).ToArray()
			};
		}

		[Test]
		public void expired_prepares_are_scavenged() {
			CheckRecords();
		}
	}
}
