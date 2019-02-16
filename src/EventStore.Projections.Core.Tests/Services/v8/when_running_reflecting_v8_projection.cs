using System;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.v8 {
	[TestFixture]
	public class when_running_reflecting_v8_projection : TestFixtureWithJsProjection {
		protected override void Given() {
			_projection = @"
                fromAll().when({$any: 
                    function(state, event) {
                        log(JSON.stringify(state) + '/' + event.bodyRaw + '/' + event.streamId + '/' + 
                            event.eventType + '/' + event.sequenceNumber + '/' + event.metadataRaw + '/' + JSON.stringify(event.metadata));
                        return {};
                    }
                });
            ";
		}

		[Test, Category("v8")]
		public void process_event_should_reflect_event() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0,
				@"{""metadata"":1}",
				@"{""a"":""b""}", out state, out emittedEvents);
			Assert.AreEqual(1, _logged.Count);
			Assert.AreEqual(@"{}/{""a"":""b""}/stream1/type1/0/{""metadata"":1}/{""metadata"":1}", _logged[0]);
		}

		[Test, Category("v8")]
		public void process_event_should_reflect_event_2() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0,
				@"{""metadata"":1}",
				@"{""a"":1}", out state, out emittedEvents);
			Assert.AreEqual(1, _logged.Count);
			Assert.AreEqual(@"{}/{""a"":1}/stream1/type1/0/{""metadata"":1}/{""metadata"":1}", _logged[0]);
		}

		[Test, Category("v8")]
		public void multiple_process_event_should_reflect_events() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0,
				@"{""metadata"":0}",
				@"{""a"":""b""}", out state, out emittedEvents);
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 40, 30), "stream1", "type1", "category", Guid.NewGuid(), 1,
				@"{""metadata"":1}",
				@"{""c"":""d""}", out state, out emittedEvents);
			Assert.AreEqual(2, _logged.Count);
			Assert.AreEqual(@"{}/{""a"":""b""}/stream1/type1/0/{""metadata"":0}/{""metadata"":0}", _logged[0]);
			Assert.AreEqual(@"{}/{""c"":""d""}/stream1/type1/1/{""metadata"":1}/{""metadata"":1}", _logged[1]);
		}

		[Test, Category("v8")]
		public void process_event_returns_true() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			var result = _stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0,
				@"{""metadata"":1}",
				@"{""a"":""b""}", out state, out emittedEvents);

			Assert.IsTrue(result);
		}

		[Test, Category("v8")]
		public void process_event_with_null_category_returns_true() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			var result = _stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", null, Guid.NewGuid(), 0,
				@"{""metadata"":1}", @"{""a"":""b""}",
				out state, out emittedEvents);

			Assert.IsTrue(result);
		}
	}
}
