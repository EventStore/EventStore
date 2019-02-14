using System;
using System.Globalization;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.v8 {
	[TestFixture]
	public class when_running_counting_v8_projection : TestFixtureWithJsProjection {
		protected override void Given() {
			_projection = @"
                fromAll().when({$any: 
                    function(state, event) {
                        state.count = state.count + 1;
                        log(state.count);
                        return state;
                    }});
            ";
			_state = @"{""count"": 0}";
		}

		[Test, Category("v8")]
		public void process_event_counts_events() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 10, 5), "stream1", "type1", "category", Guid.NewGuid(), 0, "metadata",
				@"{""a"":""b""}", out state, out emittedEvents);
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 15), "stream1", "type1", "category", Guid.NewGuid(), 1,
				"metadata",
				@"{""a"":""b""}", out state, out emittedEvents);
			Assert.AreEqual(2, _logged.Count);
			Assert.AreEqual(@"1", _logged[0]);
			Assert.AreEqual(@"2", _logged[1]);
		}

		[Test, Category("v8"), Category("Manual"), Explicit]
		public void can_handle_million_events() {
			for (var i = 0; i < 1000000; i++) {
				_logged.Clear();
				string state;
				EmittedEventEnvelope[] emittedEvents;
				_stateHandler.ProcessEvent(
					"", CheckpointTag.FromPosition(0, i * 10, i * 10 - 5), "stream" + i, "type" + i, "category",
					Guid.NewGuid(), 0,
					"metadata", @"{""a"":""" + i + @"""}", out state, out emittedEvents);
				Assert.AreEqual(1, _logged.Count);
				Assert.AreEqual((i + 1).ToString(CultureInfo.InvariantCulture), _logged[_logged.Count - 1]);
			}
		}
	}
}
