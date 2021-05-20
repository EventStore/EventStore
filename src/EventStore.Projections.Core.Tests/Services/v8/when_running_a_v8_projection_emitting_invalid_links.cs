using System;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.v8 {
	public class when_running_a_v8_projection_emitting_invalid_links : TestFixtureWithJsProjection {
		protected override void Given() {
			_projection = @"
                fromAll().when({$any: 
                    function(state, event) {
                        event.sequenceNumber = null;
                    linkTo('output-stream', event);
                    return {};
                }});
            ";
		}

		[Test, Category("v8")]
		public void process_event_ignores_emitted_event() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0,
				"metadata",
				@"{""a"":""b""}", out state, out emittedEvents);

			Assert.IsNull(emittedEvents);
		}
	}
}
