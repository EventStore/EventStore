using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.query {
	public class a_failed_projection {
		public abstract class Base : a_new_posted_projection.Base {
			protected override void Given() {
				base.Given();
				_projectionSource = "fail";
				NoOtherStreams();
			}

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
						"event", false, new byte[0], new byte[0], 100, 33.3f));
			}
		}

		[TestFixture]
		public class when_updating_query : Base {
			protected override IEnumerable<WhenStep> When() {
				foreach (var m in base.When()) yield return m;
				yield return
					(new ProjectionManagementMessage.Command.UpdateQuery(
						new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.Anonymous,
						"native:" + typeof(FakeProjection).AssemblyQualifiedName, @"", null));
			}

			[Test]
			public void the_projection_status_becomes_running() {
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
					ManagedProjectionState.Running,
					_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
						.Single()
						.Projections.Single()
						.MasterStatus);
			}
		}

		[TestFixture]
		public class when_stopping : Base {
			protected override IEnumerable<WhenStep> When() {
				foreach (var m in base.When()) yield return m;
				yield return
					(new ProjectionManagementMessage.Command.Disable(
						new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.Anonymous));
			}

			[Test]
			public void the_projection_status_becomes_faulted_disabled() {
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
					ManagedProjectionState.Stopped,
					_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
						.Single()
						.Projections.Single()
						.MasterStatus);
				Assert.AreEqual(
					false,
					_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
						.Single()
						.Projections.Single()
						.Enabled);
			}
		}

		[TestFixture]
		public class when_starting : Base {
			protected override IEnumerable<WhenStep> When() {
				foreach (var m in base.When()) yield return m;
				yield return
					(new ProjectionManagementMessage.Command.Enable(
						new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.Anonymous));
			}

			[Test]
			public void the_projection_status_becomes_running_enabled() // as we restart
			{
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
					ManagedProjectionState.Running,
					_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
						.Single()
						.Projections.Single()
						.MasterStatus);
				Assert.AreEqual(
					true,
					_consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
						.Single()
						.Projections.Single()
						.Enabled);
			}
		}
	}
}
