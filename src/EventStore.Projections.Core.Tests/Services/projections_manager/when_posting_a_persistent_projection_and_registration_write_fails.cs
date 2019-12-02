using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;
using EventStore.Projections.Core.Services.Processing;
using System.Collections;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	public class FailureConditions : IEnumerable {
		public IEnumerator GetEnumerator() {
			yield return OperationResult.CommitTimeout;
			yield return OperationResult.ForwardTimeout;
			yield return OperationResult.PrepareTimeout;
		}
	}

	[TestFixture, TestFixtureSource(typeof(FailureConditions))]
	public class
		when_posting_a_persistent_projection_and_registration_write_fails :
			TestFixtureWithProjectionCoreAndManagementServices {
		private OperationResult _failureCondition;

		public when_posting_a_persistent_projection_and_registration_write_fails(OperationResult failureCondition) {
			_failureCondition = failureCondition;
		}

		protected override void Given() {
			NoStream("$projections-test-projection-order");
			AllWritesToSucceed("$projections-test-projection-order");
			NoStream("$projections-test-projection-checkpoint");
			NoOtherStreams();
			AllWritesQueueUp();
		}

		private string _projectionName;

		protected override IEnumerable<WhenStep> When() {
			_projectionName = "test-projection";
			yield return new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid());
			yield return
				new ProjectionManagementMessage.Command.Post(
					new PublishEnvelope(_bus), ProjectionMode.Continuous, _projectionName,
					ProjectionManagementMessage.RunAs.System, "JS", @"fromAll().when({$any:function(s,e){return s;}});",
					enabled: true, checkpointsEnabled: true, emitEnabled: true, trackEmittedStreams: true);
		}

		[Test, Category("v8")]
		public void retries_creating_the_projection_only_the_specified_number_of_times_and_the_same_event_id() {
			int retryCount = 0;
			var projectionRegistrationWrite = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>()
				.Where(x => x.EventStreamId == ProjectionNamesBuilder.ProjectionsRegistrationStream).Last();
			var eventId = projectionRegistrationWrite.Events[0].EventId;
			while (projectionRegistrationWrite != null) {
				Assert.AreEqual(eventId, projectionRegistrationWrite.Events[0].EventId);
				projectionRegistrationWrite.Envelope.ReplyWith(new ClientMessage.WriteEventsCompleted(
					projectionRegistrationWrite.CorrelationId, _failureCondition,
					Enum.GetName(typeof(OperationResult), _failureCondition)));
				_queue.Process();
				projectionRegistrationWrite = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>()
					.Where(x => x.EventStreamId == ProjectionNamesBuilder.ProjectionsRegistrationStream)
					.LastOrDefault();
				if (projectionRegistrationWrite != null) {
					retryCount++;
				}

				_consumer.HandledMessages.Clear();
			}

			Assert.AreEqual(ProjectionManager.ProjectionCreationRetryCount, retryCount);
		}
	}
}
