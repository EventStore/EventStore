﻿using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.TransactionLogV2.Scavenging.Helpers;
using EventStore.Core.TransactionLogV2.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLogV2.Scavenging {
	[TestFixture]
	public class when_metastream_is_scavenged_and_read_index_is_set_to_keep_last_3_metaevents : ScavengeTestScenario {
		public when_metastream_is_scavenged_and_read_index_is_set_to_keep_last_3_metaevents() :
			base(metastreamMaxCount: 3) {
		}

		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "$$bla"),
					Rec.Prepare(0, "$$bla"),
					Rec.Prepare(0, "$$bla"),
					Rec.Prepare(0, "$$bla"),
					Rec.Prepare(0, "$$bla"),
					Rec.Commit(0, "$$bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				dbResult.Recs[0].Where((x, i) => i >= 2).ToArray()
			};
		}

		[Test]
		public void metastream_is_scavenged_correctly() {
			CheckRecords();
		}
	}
}
