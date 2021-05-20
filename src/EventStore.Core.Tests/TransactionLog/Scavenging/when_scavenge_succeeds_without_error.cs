﻿using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_scavenge_succeeds_without_error<TLogFormat, TStreamId> : ScavengeLifeCycleScenario<TLogFormat, TStreamId> {
		protected override Task When() {
			var cancellationTokenSource = new CancellationTokenSource();
			return TfChunkScavenger.Scavenge(true, true, 0, cancellationTokenSource.Token);
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
		public void scavenge_record_for_all_completed_chunks() {
			Assert.That(Log.Scavenged, Has.Count.EqualTo(2));
			Assert.That(Log.Scavenged[0].Scavenged, Is.True);
			Assert.That(Log.Scavenged[0].ChunkStart, Is.EqualTo(0));
			Assert.That(Log.Scavenged[0].ChunkEnd, Is.EqualTo(0));
			Assert.That(Log.Scavenged[1].Scavenged, Is.True);
			Assert.That(Log.Scavenged[1].ChunkStart, Is.EqualTo(1));
			Assert.That(Log.Scavenged[1].ChunkEnd, Is.EqualTo(1));
		}

		[Test]
		public void merge_record_for_all_completed_merges() {
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
