using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager {
	[TestFixture]
	public class
		when_a_core_projection_checkpoint_manager_has_been_created : TestFixtureWithCoreProjectionCheckpointManager {
		[Test]
		public void stopping_throws_invalid_operation_exception() {
			Assert.Throws<InvalidOperationException>(() => { _manager.Stopping(); });
		}

		[Test]
		public void stopped_throws_invalid_operation_exception() {
			Assert.Throws<InvalidOperationException>(() => { _manager.Stopped(); });
		}

		[Test]
		public void event_processed_throws_invalid_operation_exception() {
//            _manager.StateUpdated("", @"{""state"":""state""}");
			Assert.Throws<InvalidOperationException>(() => {
				_manager.EventProcessed(CheckpointTag.FromStreamPosition(0, "stream", 10), 77.7f);
			});
		}

		[Test]
		public void checkpoint_suggested_throws_invalid_operation_exception() {
			Assert.Throws<InvalidOperationException>(() => {
				_manager.CheckpointSuggested(CheckpointTag.FromStreamPosition(0, "stream", 10), 77.7f);
			});
		}

		[Test]
		public void ready_for_checkpoint_throws_invalid_operation_exception() {
			Assert.Throws<InvalidOperationException>(() => {
				_manager.Handle(new CoreProjectionProcessingMessage.ReadyForCheckpoint(null));
			});
		}

		[Test]
		public void can_begin_load_state() {
			_checkpointWriter.StartFrom(CheckpointTag.FromPosition(0, 0, -1), ExpectedVersion.NoStream);
		}

		[Test]
		public void can_be_started() {
			_manager.Start(CheckpointTag.FromStreamPosition(0, "stream", 10), null);
		}
	}
}
