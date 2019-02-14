using System;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.projection_checkpoint {
	[TestFixture]
	public class
		when_emitting_events_in_backward_order_to_the_same_stream_the_projection_checkpoint :
			TestFixtureWithExistingEvents {
		private ProjectionCheckpoint _checkpoint;
		private Exception _lastException;
		private TestCheckpointManagerMessageHandler _readyHandler;

		[SetUp]
		public void setup() {
			_readyHandler = new TestCheckpointManagerMessageHandler();
			_checkpoint = new ProjectionCheckpoint(
				_bus, _ioDispatcher, new ProjectionVersion(1, 0, 0), null, _readyHandler,
				CheckpointTag.FromPosition(0, 100, 50), new TransactionFilePositionTagger(0), 250, 1);
			try {
				_checkpoint.ValidateOrderAndEmitEvents(
					new[] {
						new EmittedEventEnvelope(
							new EmittedDataEvent(
								"stream1", Guid.NewGuid(), "type", true, "data", null,
								CheckpointTag.FromPosition(0, 140, 130), null))
					});
				_checkpoint.ValidateOrderAndEmitEvents(
					new[] {
						new EmittedEventEnvelope(
							new EmittedDataEvent(
								"stream1", Guid.NewGuid(), "type", true, "data2", null,
								CheckpointTag.FromPosition(0, 120, 110), null))
					});
			} catch (Exception ex) {
				_lastException = ex;
			}
		}

		[Test]
		public void throws_invalid_operation_exception() {
			Assert.Throws<InvalidOperationException>(() => {
				if (_lastException != null) throw _lastException;
			});
		}
	}
}
