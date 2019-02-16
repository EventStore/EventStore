using System;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.v8 {
	[TestFixture]
	public class when_running_bi_state_v8_projection : TestFixtureWithJsProjection {
		protected override void Given() {
			_projection = @"
                options({
                    biState: true,
                });
                fromAll().foreachStream().when({
                    type1: function(state, event) {
                        state[0].count = state[0].count + 1;
                        state[1].sharedCount = state[1].sharedCount + 1;
                        log(state[0].count);
                        log(state[1].sharedCount);
                        return state;
                    }});
            ";
			_state = @"{""count"": 0}";
			_sharedState = @"{""sharedCount"": 0}";
		}

		[Test, Category("v8")]
		public void process_event_counts_events() {
			string state;
			string sharedState;
			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 10, 5), "stream1", "type1", "category", Guid.NewGuid(), 0, "metadata",
				@"{""a"":""b""}", out state, out sharedState, out emittedEvents);
			Assert.AreEqual(2, _logged.Count);
			Assert.AreEqual(@"1", _logged[0]);
			Assert.AreEqual(@"1", _logged[1]);
		}
	}
}
