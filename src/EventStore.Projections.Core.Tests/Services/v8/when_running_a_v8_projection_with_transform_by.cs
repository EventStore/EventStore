using System;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.v8 {
	[TestFixture]
	public class when_running_a_v8_projection_with_transform_by : TestFixtureWithJsProjection {
		protected override void Given() {
			_projection = @"
                fromAll().when({
                    $any: function(state, event) {
                        state.a = '1';
                        return state;
                    }
                })
                .transformBy(function(state) {
                    state.b = '2';
                    return state;
                });
            ";
		}

		[Test, Category("v8")]
		public void transform_state_returns_correct_result() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0,
				"metadata",
				@"{}", out state, out emittedEvents);
			var result = _stateHandler.TransformStateToResult();

			Assert.IsNotNull(result);
			Assert.AreEqual(@"{""a"":""1"",""b"":""2""}", result);
		}
	}
}
