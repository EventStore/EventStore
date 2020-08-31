﻿using System.Linq;
using EventStore.Core.Tests.TransactionLogV2.Scavenging.Helpers;
using EventStore.Core.TransactionLogV2.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLogV2.Scavenging {
	[TestFixture]
	public class when_stream_is_deleted_and_explicit_transaction_spans_chunks_boundary : ScavengeTestScenario {
		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "bla"),
					Rec.Commit(0, "bla"),
					Rec.TransSt(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"))
				.Chunk(Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.TransEnd(1, "bla"),
					Rec.Commit(1, "bla"))
				.Chunk(Rec.Delete(2, "bla"),
					Rec.Commit(2, "bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				new[] {dbResult.Recs[0][2]},
				new[] {dbResult.Recs[1][6]}, // commit
				dbResult.Recs[2]
			};
		}

		[Test]
		public void first_prepare_of_transaction_is_preserved() {
			CheckRecords();
		}
	}
}
