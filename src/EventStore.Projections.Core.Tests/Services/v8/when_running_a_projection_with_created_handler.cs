using System;
using EventStore.Core.Data;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.v8 {
	[TestFixture]
	public class when_running_a_projection_with_created_handler : TestFixtureWithJsProjection {
		protected override void Given() {
			_projection = @"
                fromAll().foreachStream().when({
                    $created: function(s, e) {
                        log('handler-invoked');
                        log(e.streamId);
                        log(e.eventType);
                        log(e.body);
                        log(e.metadata);
                        emit('stream1', 'event1', {a:1});
                    }   
                });
            ";
			_state = @"{}";
		}

		[Test, Category("v8")]
		public void invokes_created_handler() {
			var e = new ResolvedEvent(
				"stream", 0, "stream", 0, false, new TFPos(1000, 900), Guid.NewGuid(), "event", true, "{}",
				"{\"m\":1}");

			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessPartitionCreated(
				"partition", CheckpointTag.FromPosition(0, 10, 5), e, out emittedEvents);

			Assert.AreEqual(5, _logged.Count);
			Assert.AreEqual(@"handler-invoked", _logged[0]);
			Assert.AreEqual(@"stream", _logged[1]);
			Assert.AreEqual(@"event", _logged[2]);
			Assert.AreEqual(@"{}", _logged[3]);
			Assert.AreEqual(@"{""m"":1}", _logged[4]);
		}

		[Test, Category("v8")]
		public void returns_emitted_events() {
			var e = new ResolvedEvent(
				"stream", 0, "stream", 0, false, new TFPos(1000, 900), Guid.NewGuid(), "event", true, "{}",
				"{\"m\":1}");

			EmittedEventEnvelope[] emittedEvents;
			_stateHandler.ProcessPartitionCreated(
				"partition", CheckpointTag.FromPosition(0, 10, 5), e, out emittedEvents);

			Assert.IsNotNull(emittedEvents);
			Assert.AreEqual(1, emittedEvents.Length);
			Assert.AreEqual("stream1", emittedEvents[0].Event.StreamId);
			Assert.AreEqual("event1", emittedEvents[0].Event.EventType);
			Assert.AreEqual("{\"a\":1}", emittedEvents[0].Event.Data);
		}
	}
}
