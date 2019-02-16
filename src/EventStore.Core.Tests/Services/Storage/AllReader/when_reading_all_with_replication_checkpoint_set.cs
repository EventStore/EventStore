using System;
using NUnit.Framework;
using EventStore.Core.Data;

namespace EventStore.Core.Tests.Services.Storage.AllReader {
	[TestFixture]
	public class when_reading_all_with_replication_checkpoint_set
		: ReadIndexTestScenario {
		long _commitPosition;

		protected override void WriteTestScenario() {
			var res = WritePrepare("ES1", 0, Guid.NewGuid(), "event-type", new string('.', 3000));
			WriteCommit(res.LogPosition, "ES1", 0);

			res = WritePrepare("ES2", 0, Guid.NewGuid(), "event-type", new string('.', 3000));
			var commit = WriteCommit(res.LogPosition, "ES2", 0);
			_commitPosition = commit.LogPosition;

			res = WritePrepare("ES2", 1, Guid.NewGuid(), "event-type", new string('.', 3000));
			WriteCommit(res.LogPosition, "ES2", 1);

			ReplicationCheckpoint.Write(_commitPosition);
		}

		[Test]
		public void should_be_able_to_read_all_backwards_and_get_events_before_replication_checkpoint() {
			var checkpoint = WriterCheckpoint.Read();
			var pos = new TFPos(checkpoint, checkpoint);
			var result = ReadIndex.ReadAllEventsBackward(pos, 10);
			Assert.AreEqual(2, result.Records.Count);
		}

		[Test]
		public void should_be_able_to_read_all_forwards_and_get_events_before_replication_checkpoint() {
			var result = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 10);
			Assert.AreEqual(2, result.Records.Count);
		}
	}
}
