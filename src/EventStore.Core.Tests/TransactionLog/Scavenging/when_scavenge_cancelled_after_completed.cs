using System.Threading;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture]
	class when_scavenge_cancelled_after_completed : ScavengeLifeCycleScenario {
		protected override void When() {
			var cancellationTokenSource = new CancellationTokenSource();

			Log.CompletedCallback += (sender, args) => cancellationTokenSource.Cancel();
			TfChunkScavenger.Scavenge(true, true, 0, cancellationTokenSource.Token).Wait();
		}

		[Test]
		public void completed_logged_with_success_result() {
			Assert.That(Log.Completed);
			Assert.That(Log.Result, Is.EqualTo(ScavengeResult.Success));
		}

		[Test]
		public void scavenge_record_for_all_completed_chunks() {
			Assert.That(Log.Scavenged, Has.Count.EqualTo(2));
			Assert.That(Log.Scavenged[0].Scavenged, Is.True);
			Assert.That(Log.Scavenged[1].Scavenged, Is.True);
		}


		[Test]
		public void merge_record_for_all_completed_merged() {
			Assert.That(Log.Merged, Has.Count.EqualTo(1));
			Assert.That(Log.Merged[0].Scavenged, Is.True);
		}


		[Test]
		public void calls_scavenge_on_the_table_index() {
			Assert.That(FakeTableIndex.ScavengeCount, Is.EqualTo(1));
		}
	}
}
