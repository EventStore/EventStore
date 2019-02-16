using System.Threading;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture]
	class when_scavenge_from_chunk_number : ScavengeLifeCycleScenario {
		protected override void When() {
			var cancellationTokenSource = new CancellationTokenSource();
			TfChunkScavenger.Scavenge(true, true, 1, cancellationTokenSource.Token).Wait();
		}

		[Test]
		public void log_started() {
			Assert.That(Log.Started);
		}

		[Test]
		public void log_completed_with_success() {
			Assert.That(Log.Completed);
			Assert.That(Log.Result, Is.EqualTo(ScavengeResult.Success));
		}

		[Test]
		public void scavenge_record_for_chunks_1_plus() {
			Assert.That(Log.Scavenged, Has.Count.EqualTo(1));
			Assert.That(Log.Scavenged[0].Scavenged, Is.True);
			Assert.That(Log.Scavenged[0].ChunkStart, Is.EqualTo(1));
			Assert.That(Log.Scavenged[0].ChunkEnd, Is.EqualTo(1));
		}

		[Test]
		public void scavenge_record_for_chunks_all() {
			Assert.That(Log.Merged, Has.Count.EqualTo(1));
			Assert.That(Log.Merged[0].Scavenged, Is.True);
			Assert.That(Log.Merged[0].ChunkStart, Is.EqualTo(0));
			Assert.That(Log.Merged[0].ChunkEnd, Is.EqualTo(1));
		}

		[Test]
		public void calls_scavenge_on_the_table_index() {
			Assert.That(FakeTableIndex.ScavengeCount, Is.EqualTo(1));
		}
	}
}
