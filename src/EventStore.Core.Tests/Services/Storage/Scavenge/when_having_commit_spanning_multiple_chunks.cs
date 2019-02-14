using System;
using System.Collections.Generic;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture]
	public class when_having_commit_spanning_multiple_chunks : ReadIndexTestScenario {
		private List<LogRecord> _survivors;
		private List<LogRecord> _scavenged;

		protected override void WriteTestScenario() {
			_survivors = new List<LogRecord>();
			_scavenged = new List<LogRecord>();

			var transPos = WriterCheckpoint.ReadNonFlushed();

			for (int i = 0; i < 10; ++i) {
				long tmp;
				var r = LogRecord.Prepare(WriterCheckpoint.ReadNonFlushed(),
					Guid.NewGuid(),
					Guid.NewGuid(),
					transPos,
					i,
					"s1",
					i == 0 ? -1 : -2,
					PrepareFlags.Data | (i == 9 ? PrepareFlags.TransactionEnd : PrepareFlags.None),
					"event-type",
					new byte[3],
					new byte[3]);
				Assert.IsTrue(Writer.Write(r, out tmp));
				Writer.CompleteChunk();

				_scavenged.Add(r);
			}

			var r2 = WriteCommit(transPos, "s1", 0);
			_survivors.Add(r2);

			Writer.CompleteChunk();

			var r3 = WriteDeletePrepare("s1");
			_survivors.Add(r3);

			Writer.CompleteChunk();

			var r4 = WriteDeleteCommit(r3);
			_survivors.Add(r4);

			Writer.CompleteChunk();

			Scavenge(completeLast: false, mergeChunks: true);

			Assert.AreEqual(13, _survivors.Count + _scavenged.Count);
		}

		[Test]
		public void all_chunks_are_merged_and_scavenged() {
			foreach (var rec in _scavenged) {
				var chunk = Db.Manager.GetChunkFor(rec.LogPosition);
				Assert.IsFalse(chunk.TryReadAt(rec.LogPosition).Success);
			}

			foreach (var rec in _survivors) {
				var chunk = Db.Manager.GetChunkFor(rec.LogPosition);
				var res = chunk.TryReadAt(rec.LogPosition);
				Assert.IsTrue(res.Success);
				Assert.AreEqual(rec, res.LogRecord);
			}
		}
	}
}
