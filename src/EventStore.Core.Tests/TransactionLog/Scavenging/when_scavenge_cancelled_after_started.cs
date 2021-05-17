﻿using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_scavenge_cancelled_after_started<TLogFormat, TStreamId> : ScavengeLifeCycleScenario<TLogFormat, TStreamId> {
		protected override async Task When() {
			var cancellationTokenSource = new CancellationTokenSource();

			Log.StartedCallback += (sender, args) => cancellationTokenSource.Cancel();
			await TfChunkScavenger.Scavenge(false, true, 0, cancellationTokenSource.Token);
		}

		[Test]
		public void completed_logged_with_stopped_result() {
			Assert.That(Log.Completed);
			Assert.That(Log.Result, Is.EqualTo(ScavengeResult.Stopped));
		}

		[Test]
		public void no_chunks_scavenged() {
			Assert.That(Log.Scavenged, Is.Empty);
		}

		[Test]
		public void doesnt_call_scavenge_on_the_table_index() {
			Assert.That(FakeTableIndex.ScavengeCount, Is.EqualTo(0));
		}
	}
}
