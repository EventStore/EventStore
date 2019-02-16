using System;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.projection_checkpoint {
	[TestFixture]
	public class the_non_started_checkpoint : TestFixtureWithExistingEvents {
		private ProjectionCheckpoint _checkpoint;
		private TestCheckpointManagerMessageHandler _readyHandler;

		[SetUp]
		public void setup() {
			_readyHandler = new TestCheckpointManagerMessageHandler();
			_checkpoint = new ProjectionCheckpoint(
				_bus, _ioDispatcher, new ProjectionVersion(1, 0, 0), null, _readyHandler,
				CheckpointTag.FromPosition(0, 100, 50), new TransactionFilePositionTagger(0), 250, 1);
		}

		[Test]
		public void prepare_throws_invalid_operation_exception() {
			Assert.Throws<InvalidOperationException>(() => {
				_checkpoint.Prepare(CheckpointTag.FromPosition(0, 200, 150));
			});
		}
	}
}
