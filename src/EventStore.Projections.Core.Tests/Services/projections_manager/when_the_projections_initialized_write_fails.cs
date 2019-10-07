using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messages;
using NUnit.Framework;
using EventStore.Projections.Core.Services.Processing;
using System.Collections;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	[TestFixture, TestFixtureSource(typeof(FailureConditions))]
	public class
		when_writing_the_projections_initialized_event_fails : TestFixtureWithProjectionCoreAndManagementServices {
		private class FailureConditions : IEnumerable {
			public IEnumerator GetEnumerator() {
				yield return OperationResult.CommitTimeout;
				yield return OperationResult.ForwardTimeout;
				yield return OperationResult.PrepareTimeout;
			}
		}

		private OperationResult _failureCondition;

		public when_writing_the_projections_initialized_event_fails(OperationResult failureCondition) {
			_failureCondition = failureCondition;
		}

		protected override void Given() {
			AllWritesQueueUp();
			NoStream(ProjectionNamesBuilder.ProjectionsRegistrationStream);
		}

		protected override bool GivenInitializeSystemProjections() {
			return false;
		}

		protected override IEnumerable<WhenStep> When() {
			yield return new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid());
		}

		[Test, Category("v8")]
		public void retries_writing_with_the_same_event_id() {
			int retryCount = 0;
			var projectionsInitializedWrite = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Where(x =>
				x.EventStreamId == ProjectionNamesBuilder.ProjectionsRegistrationStream &&
				x.Events[0].EventType == ProjectionEventTypes.ProjectionsInitialized).Last();
			var eventId = projectionsInitializedWrite.Events[0].EventId;
			while (retryCount < 5) {
				Assert.AreEqual(eventId, projectionsInitializedWrite.Events[0].EventId);
				projectionsInitializedWrite.Envelope.ReplyWith(new ClientMessage.WriteEventsCompleted(
					projectionsInitializedWrite.CorrelationId, _failureCondition,
					Enum.GetName(typeof(OperationResult), _failureCondition)));
				_queue.Process();
				projectionsInitializedWrite = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Where(x =>
					x.EventStreamId == ProjectionNamesBuilder.ProjectionsRegistrationStream &&
					x.Events[0].EventType == ProjectionEventTypes.ProjectionsInitialized).LastOrDefault();
				if (projectionsInitializedWrite != null) {
					retryCount++;
				}

				_consumer.HandledMessages.Clear();
			}
		}
	}
}
