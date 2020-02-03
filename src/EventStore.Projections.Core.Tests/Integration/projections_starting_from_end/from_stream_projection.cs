using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Integration.projections_starting_from_end {
	public class from_stream_projection {
		[TestFixture]
		public class when_subscribing_from_end : specification_with_a_v8_projection_posted {
			private readonly string _inputStream = "input-stream";
			private readonly string _projectedStream = "projected-stream";
			private readonly ProjectionNamesBuilder _namesBuilder;

			public when_subscribing_from_end() {
				_namesBuilder = ProjectionNamesBuilder.CreateForTest(_projectionName);
			}

			protected override void GivenEvents() {
				ExistingEvent(_inputStream, "test", "", "{\"a\":1}");
				ExistingEvent(_inputStream, "test", "", "{\"a\":2}");
				ExistingEvent(_inputStream, "test", "", "{\"a\":3}");
			}

			protected override IEnumerable<WhenStep> When() {
				foreach (var e in base.When()) yield return e;

				// Write some more events
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":1}");
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":2}");
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":3}");
			}

			protected override ProjectionManagementMessage.Command.Post GivenProjection() {
				return CreateNewProjectionMessage(
					_projectionName,
					"fromStream(\"" + _inputStream + "\").when({$any: function(s,e) { linkTo(\"" +
					_projectedStream + "\", e) }})",
					subscribeFromEnd: true);
			}

			[Test]
			public void should_handle_new_events() {
				AssertStreamContains(_projectedStream, "3@input-stream", "4@input-stream", "5@input-stream");
			}

			[Test]
			public void should_not_handle_old_events() {
				AssertStreamDoesNotContain(_projectedStream, "0@input-stream", "1@input-stream", "2@input-stream");
			}

			[Test]
			public void should_write_checkpoint_on_first_handled_event() {
				var checkpointEvents = _streams[_namesBuilder.MakeCheckpointStreamName()];
				Assert.AreEqual(1, checkpointEvents.Count);
				var checkpointMetadata = Encoding.UTF8.GetString(checkpointEvents[0].Metadata);

				Assert.True(checkpointMetadata.Contains("\"" + _inputStream + "\":3"),
					$"Expected checkpoint to contain '{_inputStream}:3', but was: '{checkpointMetadata}'");
			}
		}

		[TestFixture]
		public class when_subscribing_from_end_and_reset : specification_with_a_v8_projection_posted {
			private readonly string _inputStream = "input-stream";
			private readonly string _projectedStream = "projected-stream";
			private readonly ProjectionNamesBuilder _namesBuilder;

			public when_subscribing_from_end_and_reset() {
				_namesBuilder = ProjectionNamesBuilder.CreateForTest(_projectionName);
			}

			protected override IEnumerable<WhenStep> When() {
				foreach (var e in base.When()) yield return e;

				// Write some more events
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":1}");
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":2}");
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":3}");

				yield return new ProjectionManagementMessage.Command.Reset(new NoopEnvelope(), _projectionName,
					ProjectionManagementMessage.RunAs.System);

				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":4}");
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":5}");
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":6}");
			}

			protected override ProjectionManagementMessage.Command.Post GivenProjection() {
				return CreateNewProjectionMessage(
					_projectionName,
					"fromStream(\"" + _inputStream + "\").when({$any: function(s,e) { linkTo(\"" +
					_projectedStream + "\", e) }})",
					subscribeFromEnd: true);
			}

			[Test]
			public void should_have_truncated_output_stream() {
				AssertStreamMetadata(_projectedStream, "\"$tb\":3");
			}

			[Test]
			public void should_not_handle_original_events_again() {
				var eventsData = GetStreamEventData(_projectedStream);

				var notExpected = new[] {"0@input-stream", "1@input-stream", "2@input-stream"};
				var duplicates = notExpected.Where(v => eventsData.Count(x => x == v) > 1).ToArray();

				Assert.That(duplicates.Length == 0,
					$"{_projectedStream} contains duplicates : {duplicates.Aggregate("", (a, v) => a + " " + v)}");
			}

			[Test]
			public void should_handle_new_events() {
				AssertStreamContains(_projectedStream, "3@input-stream", "4@input-stream", "5@input-stream");
			}

			[Test]
			public void should_write_checkpoint_on_first_handled_event_after_reset() {
				var checkpointEvents = _streams[_namesBuilder.MakeCheckpointStreamName()];

				// Projection checkpoints when it is reset
				Assert.AreEqual(3, checkpointEvents.Count);
				var checkpointMetadata = Encoding.UTF8.GetString(checkpointEvents.Last().Metadata);

				Assert.True(checkpointMetadata.Contains("\"" + _inputStream + "\":3"),
					$"Expected checkpoint to contain '{_inputStream}:3', but was: '{checkpointMetadata}'");
			}
		}

		[TestFixture]
		public class when_subscribing_from_end_and_restarted : specification_with_a_v8_projection_posted {
			private readonly string _inputStream = "input-stream";
			private readonly string _projectedStream = "projected-stream";

			protected override IEnumerable<WhenStep> When() {
				foreach (var e in base.When()) yield return e;

				// Write an event to force the checkpoint
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":1}");

				yield return new ProjectionManagementMessage.Command.Disable(new NoopEnvelope(), _projectionName,
					ProjectionManagementMessage.RunAs.System);

				// Write events between stopping and starting the projection
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":2}");
				yield return CreateWriteEvent(_inputStream, "test", "{\"b\":3}");

				// Start the projection up again
				yield return new ProjectionManagementMessage.Command.Enable(new NoopEnvelope(), _projectionName,
					ProjectionManagementMessage.RunAs.System);
			}

			protected override ProjectionManagementMessage.Command.Post GivenProjection() {
				return CreateNewProjectionMessage(
					_projectionName,
					"fromStream(\"" + _inputStream + "\").when({$any: function(s,e) { linkTo(\"" +
					_projectedStream + "\", e) }})",
					subscribeFromEnd: true);
			}

			[Test]
			public void should_handle_original_event() {
				AssertStreamContains(_projectedStream, "0@input-stream");
			}

			[Test]
			public void should_handle_events_posted_while_disabled() {
				AssertStreamContains(_projectedStream, "1@input-stream", "2@input-stream");
			}
		}
	}
}
