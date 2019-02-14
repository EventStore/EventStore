using System;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.v8 {
	[TestFixture]
	public class when_running_emitting_v8_projection : TestFixtureWithJsProjection {
		protected override void Given() {
			_projection = @"
                fromAll().when({$any: 
                    function(state, event) {
                    emit('output-stream' + event.sequenceNumber, 'emitted-event' + event.sequenceNumber, {a: JSON.parse(event.bodyRaw).a});
                    return {};
                }});
            ";
		}

		[Test, Category("v8")]
		public void process_event_returns_true() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			var result = _stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0,
				"metadata",
				@"{""a"":""b""}", out state, out emittedEvents);

			Assert.IsTrue(result);
		}

		[Test, Category("v8")]
		public void process_event_returns_emitted_event() {
			string state;
			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessEvent(
				"", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0,
				"metadata",
				@"{""a"":""b""}", out state, out emittedEvents);

			Assert.IsNotNull(emittedEvents);
			Assert.AreEqual(1, emittedEvents.Length);
			Assert.AreEqual("emitted-event0", emittedEvents[0].Event.EventType);
			Assert.AreEqual("output-stream0", emittedEvents[0].Event.StreamId);
			Assert.AreEqual(@"{""a"":""b""}", emittedEvents[0].Event.Data);
		}

		[Test, Category("v8"), Category("Manual"), Explicit]
		public void can_pass_though_millions_of_events() {
			for (var i = 0; i < 100000000; i++) {
				string state;
				EmittedEventEnvelope[] emittedEvents;
				_stateHandler.ProcessEvent(
					"", CheckpointTag.FromPosition(0, i * 10 + 20, i * 10 + 10), "stream" + i, "type" + i, "category",
					Guid.NewGuid(), i,
					"metadata", @"{""a"":""" + i + @"""}", out state, out emittedEvents);

				Assert.IsNotNull(emittedEvents);
				Assert.AreEqual(1, emittedEvents.Length);
				Assert.AreEqual("emitted-event" + i, emittedEvents[0].Event.EventType);
				Assert.AreEqual("output-stream" + i, emittedEvents[0].Event.StreamId);
				Assert.AreEqual(@"{""a"":""" + i + @"""}", emittedEvents[0].Event.Data);

				if (i % 10000 == 0) {
					Teardown();
					Setup(); // recompile..
					Console.Write(".");
				}
			}
		}
	}
}
