using System;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.v8 {
	[TestFixture]
	public class when_initializing_state : TestFixtureWithJsProjection {
		protected override void Given() {
			_projection = @"
                fromAll().when({
                    $init: function() {
                        return { test: '1' };
                    },
                    
                    type1: function(state, event) {
                        return state;
                    },
                }).transformBy(function(s) { return s;});
            ";
		}

		[Test, Category("v8")]
		public void process_event_should_return_initialized_state() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category",
				Guid.NewGuid(), 0, "metadata", @"{""a"":""b""}", out state, out emittedEvents);
			Assert.IsTrue(state.Contains("\"test\":\"1\""));
		}

		[Test, Category("v8")]
		public void transform_state_should_return_initialized_state() {
			var result = _stateHandler.TransformStateToResult();
			Assert.IsTrue(result.Contains("\"test\":\"1\""));
		}
	}
}
