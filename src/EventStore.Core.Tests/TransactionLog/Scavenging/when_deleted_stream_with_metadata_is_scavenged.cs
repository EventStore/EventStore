﻿using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture]
	public class when_deleted_stream_with_metadata_is_scavenged : ScavengeTestScenario {
		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(10, null, null, null, null)),
					Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(2, null, null, null, null)),
					Rec.Commit(0, "$$bla"),
					Rec.Delete(1, "bla"),
					Rec.Commit(1, "bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				dbResult.Recs[0].Where((x, i) => i >= 3).ToArray()
			};
		}

		[Test]
		public void metastream_is_scavenged_as_well() {
			CheckRecords();
		}
	}
}
