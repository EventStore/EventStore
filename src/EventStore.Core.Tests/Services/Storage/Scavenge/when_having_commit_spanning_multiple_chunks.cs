using System;
using System.Collections.Generic;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint), Ignore = "Explicit transactions are not supported yet by Log V3")]
	public class when_having_commit_spanning_multiple_chunks<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private List<ILogRecord> _survivors;
		private List<ILogRecord> _scavenged;

		protected override void WriteTestScenario() {
			_survivors = new List<ILogRecord>();
			_scavenged = new List<ILogRecord>();

			GetOrReserve("s1", out var s1StreamId, out _);
			GetOrReserveEventType("event-type", out var eventTypeId, out _);
			var transPos = Writer.Position;

			for (int i = 0; i < 10; ++i) {
				var r = LogRecord.Prepare(_recordFactory, Writer.Position,
					Guid.NewGuid(),
					Guid.NewGuid(),
					transPos,
					i,
					s1StreamId,
					i == 0 ? -1 : -2,
					PrepareFlags.Data | (i == 9 ? PrepareFlags.TransactionEnd : PrepareFlags.None),
					eventTypeId,
					new byte[3],
					new byte[3]);
				Assert.IsTrue(Writer.Write(r, out _));
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
				Assert.IsFalse(chunk.TryReadAt(rec.LogPosition, couldBeScavenged: true, tracker: ITransactionFileTracker.NoOp).Success);
			}

			foreach (var rec in _survivors) {
				var chunk = Db.Manager.GetChunkFor(rec.LogPosition);
				var res = chunk.TryReadAt(rec.LogPosition, couldBeScavenged: false, tracker: ITransactionFileTracker.NoOp);
				Assert.IsTrue(res.Success);
				Assert.AreEqual(rec, res.LogRecord);
			}
		}
	}
}
