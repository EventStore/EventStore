using System;
using EventStore.Core.Data;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Standard;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.handlers {
	public static class categorize_events_by_stream_path {
		[TestFixture]
		public class when_handling_simple_event {
			private CategorizeEventsByStreamPath _handler;
			private string _state;
			private EmittedEventEnvelope[] _emittedEvents;
			private bool _result;

			[SetUp]
			public void when() {
				_handler = new CategorizeEventsByStreamPath("-", Console.WriteLine);
				_handler.Initialize();
				string sharedState;
				_result = _handler.ProcessEvent(
					"", CheckpointTag.FromPosition(0, 200, 150), null,
					new ResolvedEvent(
						"cat1-stream1", 10, "cat1-stream1", 10, false, new TFPos(200, 150), Guid.NewGuid(),
						"event_type", true, "{}", "{}"), out _state, out sharedState, out _emittedEvents);
			}

			[Test]
			public void result_is_true() {
				Assert.IsTrue(_result);
			}

			[Test]
			public void state_stays_null() {
				Assert.IsNull(_state);
			}

			[Test]
			public void emits_correct_link() {
				Assert.NotNull(_emittedEvents);
				Assert.AreEqual(1, _emittedEvents.Length);
				var @event = _emittedEvents[0].Event;
				Assert.AreEqual("$>", @event.EventType);
				Assert.AreEqual("$ce-cat1", @event.StreamId);
				Assert.AreEqual("10@cat1-stream1", @event.Data);
			}
		}

		[TestFixture]
		public class when_handling_link_to_event {
			private CategorizeEventsByStreamPath _handler;
			private string _state;
			private EmittedEventEnvelope[] _emittedEvents;
			private bool _result;

			[SetUp]
			public void when() {
				_handler = new CategorizeEventsByStreamPath("-", Console.WriteLine);
				_handler.Initialize();
				string sharedState;
				_result = _handler.ProcessEvent(
					"", CheckpointTag.FromPosition(0, 200, 150), null,
					new ResolvedEvent(
						"cat2-stream2", 20, "cat2-stream2", 20, true, new TFPos(200, 150), Guid.NewGuid(),
						"$>", true, "10@cat1-stream1", "{}"), out _state, out sharedState, out _emittedEvents);
			}

			[Test]
			public void result_is_true() {
				Assert.IsTrue(_result);
			}

			[Test]
			public void state_stays_null() {
				Assert.IsNull(_state);
			}

			[Test]
			public void emits_correct_link() {
				Assert.NotNull(_emittedEvents);
				Assert.AreEqual(1, _emittedEvents.Length);
				var @event = _emittedEvents[0].Event;
				Assert.AreEqual("$>", @event.EventType);
				Assert.AreEqual("$ce-cat2", @event.StreamId);
				Assert.AreEqual("10@cat1-stream1", @event.Data);
			}
		}
	}
}
