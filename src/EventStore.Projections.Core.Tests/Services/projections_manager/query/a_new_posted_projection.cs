using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.query {
	public static class a_new_posted_projection {
		public abstract class Base : TestFixtureWithProjectionCoreAndManagementServices {
			protected string _projectionName;
			protected string _projectionSource;
			protected Type _fakeProjectionType;
			protected ProjectionMode _projectionMode;
			protected bool _checkpointsEnabled;
			protected bool _trackEmittedStreams;
			protected bool _emitEnabled;

			protected override void Given() {
				base.Given();

				_projectionName = "test-projection";
				_projectionSource = @"";
				_fakeProjectionType = typeof(FakeProjection);
				_projectionMode = ProjectionMode.Transient;
				_checkpointsEnabled = false;
				_trackEmittedStreams = false;
				_emitEnabled = false;
				NoOtherStreams();
			}

			protected override IEnumerable<WhenStep> When() {
				yield return (new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid()));
				yield return
					(new ProjectionManagementMessage.Command.Post(
						new PublishEnvelope(_bus), _projectionMode, _projectionName,
						ProjectionManagementMessage.RunAs.System, "native:" + _fakeProjectionType.AssemblyQualifiedName,
						_projectionSource, enabled: true, checkpointsEnabled: _checkpointsEnabled,
						emitEnabled: _emitEnabled, trackEmittedStreams: _trackEmittedStreams));
			}
		}

		[TestFixture]
		public class when_get_query : Base {
			protected override IEnumerable<WhenStep> When() {
				foreach (var m in base.When()) yield return m;
				yield return
					(new ProjectionManagementMessage.Command.GetQuery(
						new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.Anonymous));
			}

			[Test]
			public void returns_correct_source() {
				Assert.AreEqual(
					1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Count());
				var projectionQuery =
					_consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Single();
				Assert.AreEqual(_projectionName, projectionQuery.Name);
				Assert.AreEqual("", projectionQuery.Query);
			}
		}

		[TestFixture]
		public class when_get_state : Base {
			protected override IEnumerable<WhenStep> When() {
				foreach (var m in base.When()) yield return m;
				yield return (
					new ProjectionManagementMessage.Command.GetState(new PublishEnvelope(_bus), _projectionName, ""));
			}

			[Test]
			public void returns_correct_state() {
				Assert.AreEqual(
					1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Count());
				Assert.AreEqual(
					_projectionName,
					_consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().Name);
				Assert.AreEqual(
					"", _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().State);
			}
		}

		[TestFixture]
		public class when_failing : Base {
			protected override IEnumerable<WhenStep> When() {
				foreach (var m in base.When()) yield return m;
				var readerAssignedMessage =
					_consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.ReaderAssignedReader>()
						.LastOrDefault();
				Assert.IsNotNull(readerAssignedMessage);
				var reader = readerAssignedMessage.ReaderId;

				yield return
					(ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
						reader, new TFPos(100, 50), new TFPos(100, 50), "stream", 1, "stream", 1, false, Guid.NewGuid(),
						"fail", false, new byte[0], new byte[0], 100, 33.3f));
			}

			[Test]
			public void publishes_faulted_message() {
				Assert.AreEqual(1, _consumer.HandledMessages.OfType<CoreProjectionStatusMessage.Faulted>().Count());
			}

			[Test]
			public void the_projection_status_becomes_faulted() {
				_manager.Handle(
					new ProjectionManagementMessage.Command.GetStatistics(
						new PublishEnvelope(_bus), null, _projectionName, false));

				Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count());
				Assert.AreEqual(
					1,
					_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
						.Single()
						.Projections.Length);
				Assert.AreEqual(
					_projectionName,
					_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
						.Single()
						.Projections.Single()
						.Name);
				Assert.AreEqual(
					ManagedProjectionState.Faulted,
					_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
						.Single()
						.Projections.Single()
						.LeaderStatus);
			}
		}
	}
}
