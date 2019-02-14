using System.Collections.Generic;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager.multi_stream {
	[TestFixture]
	public class when_starting_and_read_prerecorded_events_successfully : with_multi_stream_checkpoint_manager,
		IHandle<CoreProjectionProcessingMessage.PrerecordedEventsLoaded> {
		private ManualResetEventSlim _mre = new ManualResetEventSlim();
		private CoreProjectionProcessingMessage.PrerecordedEventsLoaded _eventsLoadedMessage;

		public override void When() {
			_bus.Subscribe<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>(this);

			_checkpointManager.Initialize();
			var positions = new Dictionary<string, long> {{"a", 1}, {"b", 1}, {"c", 1}};
			_checkpointManager.BeginLoadPrerecordedEvents(CheckpointTag.FromStreamPositions(0, positions));

			if (!_mre.Wait(10000)) {
				Assert.Fail("Timed out waiting for pre recorded events loaded message");
			}
		}

		public void Handle(CoreProjectionProcessingMessage.PrerecordedEventsLoaded message) {
			_eventsLoadedMessage = message;
			_mre.Set();
		}

		[Test]
		public void should_send_prerecorded_events_message() {
			Assert.IsNotNull(_eventsLoadedMessage);
		}
	}
}
