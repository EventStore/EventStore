using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_scavenge_throws_exception_processing_chunk<TLogFormat, TStreamId> : ScavengeLifeCycleScenario<TLogFormat, TStreamId> {
		protected override Task When() {
			var cancellationTokenSource = new CancellationTokenSource();

			Log.ChunkScavenged += (sender, args) => {
				if (args.Scavenged)
					throw new Exception("Expected exception.");
			};

			return TfChunkScavenger.Scavenge(true, true, 0, cancellationTokenSource.Token);
		}

		[Test]
		public void no_exception_is_thrown_to_caller() {
			Assert.That(Log.Completed);
			Assert.That(Log.Result, Is.EqualTo(ScavengeResult.Failed));
		}

		[Test]
		public void doesnt_call_scavenge_on_the_table_index() {
			Assert.That(FakeTableIndex.ScavengeCount, Is.EqualTo(0));
		}
	}
}
