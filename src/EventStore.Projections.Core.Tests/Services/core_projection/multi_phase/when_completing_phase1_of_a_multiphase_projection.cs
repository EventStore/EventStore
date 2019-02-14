using System.Linq;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.multi_phase {
	[TestFixture]
	class when_completing_phase1_of_a_multiphase_projection : specification_with_multi_phase_core_projection {
		protected override void When() {
			_coreProjection.Start();
			Phase1.Complete();
		}

		[Test]
		public void stops_phase1_checkpoint_manager() {
			Assert.IsTrue(Phase1CheckpointManager.Stopped_);
		}

		[Test]
		public void initializes_phase2() {
			Assert.IsTrue(Phase2.InitializedFromCheckpoint);
		}

		[Test]
		public void updates_checkpoint_tag_phase() {
			Assert.AreEqual(1, _coreProjection.LastProcessedEventPosition.Phase);
		}

		[Test]
		public void publishes_subscribe_message() {
			Assert.AreEqual(1, Phase2.SubscribeInvoked);
		}
	}
}
