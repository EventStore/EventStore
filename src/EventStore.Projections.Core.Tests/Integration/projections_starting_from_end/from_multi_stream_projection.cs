using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Integration.projections_starting_from_end {
	public class multi_stream_projection {
		[TestFixture]
		public class when_subscribing_from_end : specification_with_a_v8_projection_posted {
			private readonly string _projectedStream = "projected-stream";
			private readonly ProjectionNamesBuilder _namesBuilder;

			public when_subscribing_from_end() {
				_namesBuilder = ProjectionNamesBuilder.CreateForTest(_projectionName);
			}

			protected override void GivenEvents() {
				ExistingEvent("stream-1", "test", "", "{\"a\":1}");
				ExistingEvent("stream-2", "test", "", "{\"a\":2}");
				ExistingEvent("stream-3", "test", "", "{\"a\":3}");
			}

			protected override IEnumerable<WhenStep> When() {
				foreach (var e in base.When()) yield return e;

				// Write some more events
				yield return CreateWriteEvent("stream-1", "test", "{\"b\":1}");
				yield return CreateWriteEvent("stream-2", "test", "{\"b\":2}");
				yield return CreateWriteEvent("stream-3", "test", "{\"b\":3}");
			}

			protected override ProjectionManagementMessage.Command.Post GivenProjection() {
				return CreateNewProjectionMessage(
					_projectionName,
					"fromStreams(\"stream-1\", \"stream-2\", \"stream-3\").when({$any: function(s,e) { linkTo(\"" +
					_projectedStream + "\", e) }})",
					subscribeFromEnd: true);
			}

			[Test]
			public void should_handle_new_events() {
				AssertStreamContains(_projectedStream, "1@stream-1", "1@stream-2", "1@stream-3");
			}

			[Test]
			public void should_not_handle_old_events() {
				AssertStreamDoesNotContain(_projectedStream, "0@stream-1", "0@stream-2", "0@stream-3");
			}

			[Test]
			public void should_write_checkpoint_on_first_handled_event() {
				var checkpointEvents = _streams[_namesBuilder.MakeCheckpointStreamName()];
				Assert.AreEqual(1, checkpointEvents.Count);
				var checkpointMetadata = Encoding.UTF8.GetString(checkpointEvents[0].Metadata);

				var expectedCheckpoint = "\"stream-1\":1,\"stream-2\":-1,\"stream-3\":-1";
				Assert.True(checkpointMetadata.Contains(expectedCheckpoint),
					$"Expected checkpoint to contain '{expectedCheckpoint}', but was: '{checkpointMetadata}'");
			}
		}

		[TestFixture]
		public class when_subscribing_from_end_and_restarted : specification_with_a_v8_projection_posted {
			private readonly string _projectedStream = "projected-stream";

			protected override IEnumerable<WhenStep> When() {
				foreach (var e in base.When()) yield return e;

				// Write an event to force a checkpoint
				yield return CreateWriteEvent("stream-1", "test", "{\"b\":1}");

				yield return new ProjectionManagementMessage.Command.Disable(new NoopEnvelope(), _projectionName,
					ProjectionManagementMessage.RunAs.System);

				// Write events between stopping and starting the projection
				yield return CreateWriteEvent("stream-2", "test", "{\"b\":2}");
				yield return CreateWriteEvent("stream-3", "test", "{\"b\":3}");

				// Start the projection up again
				yield return new ProjectionManagementMessage.Command.Enable(new NoopEnvelope(), _projectionName,
					ProjectionManagementMessage.RunAs.System);
			}

			protected override ProjectionManagementMessage.Command.Post GivenProjection() {
				return CreateNewProjectionMessage(
					_projectionName,
					"fromStreams(\"stream-1\", \"stream-2\", \"stream-3\").when({$any: function(s,e) { linkTo(\"" +
					_projectedStream + "\", e) }})",
					subscribeFromEnd: true);
			}

			[Test]
			public void should_handle_original_event() {
				AssertStreamContains(_projectedStream, "0@stream-1");
			}

			[Test]
			public void should_handle_events_posted_while_disabled() {
				AssertStreamContains(_projectedStream, "0@stream-2", "0@stream-3");
			}
		}

		[TestFixture]
		public class when_subscribing_from_end_and_reset : specification_with_a_v8_projection_posted {
			private readonly string _projectedStream = "projected-stream";
			private readonly ProjectionNamesBuilder _namesBuilder;

			public when_subscribing_from_end_and_reset() {
				_namesBuilder = ProjectionNamesBuilder.CreateForTest(_projectionName);
			}

			protected override IEnumerable<WhenStep> When() {
				foreach (var e in base.When()) yield return e;

				yield return CreateWriteEvent("stream-1", "test", "{\"b\":1}");
				yield return CreateWriteEvent("stream-2", "test", "{\"b\":2}");
				yield return CreateWriteEvent("stream-3", "test", "{\"b\":3}");

				yield return new ProjectionManagementMessage.Command.Reset(new NoopEnvelope(), _projectionName,
					ProjectionManagementMessage.RunAs.System);

				yield return CreateWriteEvent("stream-1", "test", "{\"b\":1}");
				yield return CreateWriteEvent("stream-2", "test", "{\"b\":2}");
				yield return CreateWriteEvent("stream-3", "test", "{\"b\":3}");
			}

			protected override ProjectionManagementMessage.Command.Post GivenProjection() {
				return CreateNewProjectionMessage(
					_projectionName,
					"fromStreams(\"stream-1\", \"stream-2\", \"stream-3\").when({$any: function(s,e) { linkTo(\"" +
					_projectedStream + "\", e) }})",
					subscribeFromEnd: true);
			}

			[Test]
			public void should_not_handle_original_events_again() {
				var eventsData = GetStreamEventData(_projectedStream);

				var notExpected = new[] {"0@stream-1", "0@stream-2", "0@stream-3"};
				var duplicates = notExpected.Where(v => eventsData.Count(x => x == v) > 1).ToArray();

				Assert.That(duplicates.Length == 0,
					$"{_projectedStream} contains duplicates : {duplicates.Aggregate("", (a, v) => a + " " + v)}");
			}

			[Test]
			public void should_handle_new_events() {
				AssertStreamContains(_projectedStream, "1@stream-1", "1@stream-2", "1@stream-3");
			}

			[Test]
			public void should_write_checkpoint_on_first_handled_event() {
				var checkpointEvents = _streams[_namesBuilder.MakeCheckpointStreamName()];

				// Projection checkpoints when it is reset
				Assert.AreEqual(3, checkpointEvents.Count);
				var checkpointMetadata = Encoding.UTF8.GetString(checkpointEvents[0].Metadata);

				var expectedCheckpoint = "\"stream-1\":0,\"stream-2\":-1,\"stream-3\":-1";
				Assert.True(checkpointMetadata.Contains(expectedCheckpoint),
					$"Expected checkpoint to contain '{expectedCheckpoint}', but was: '{checkpointMetadata}'");
			}
		}
	}
}
