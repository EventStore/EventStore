using System;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.Data;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.position_tagging.transaction_file_position_tagger {
	[TestFixture]
	public class when_updating_transaction_file_postion_tracker {
		private PositionTagger _tagger;
		private PositionTracker _positionTracker;

		[SetUp]
		public void When() {
			// given
			_tagger = new TransactionFilePositionTagger(0);
			_positionTracker = new PositionTracker(_tagger);
			var newTag = CheckpointTag.FromPosition(0, 100, 50);
			_positionTracker.UpdateByCheckpointTagInitial(newTag);
		}

		[Test]
		public void checkpoint_tag_is_for_correct_position() {
			Assert.AreEqual(new TFPos(100, 50), _positionTracker.LastTag.Position);
		}

		[Test]
		public void cannot_update_to_the_same_postion() {
			Assert.Throws<InvalidOperationException>(() => {
				var newTag = CheckpointTag.FromPosition(0, 100, 50);
				_positionTracker.UpdateByCheckpointTagForward(newTag);
			});
		}
	}
}
