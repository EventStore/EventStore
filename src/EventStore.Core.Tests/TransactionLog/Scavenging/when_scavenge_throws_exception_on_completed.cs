using System;
using System.Threading;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture]
	class when_scavenge_throws_exception_on_completed : ScavengeLifeCycleScenario {
		protected override void When() {
			var cancellationTokenSource = new CancellationTokenSource();

			Log.CompletedCallback += (sender, args) => { throw new Exception("Expected exception."); };
			TfChunkScavenger.Scavenge(true, true, 0, cancellationTokenSource.Token).Wait();
		}

		[Test]
		public void no_exception_is_thrown() {
			Assert.That(Log.Completed);
			Assert.That(Log.Result, Is.EqualTo(ScavengeResult.Success));
		}
	}
}
