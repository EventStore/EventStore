using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using EventStore.ClientAPI.Common.Utils;
using System.Collections;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	public class SystemProjectionNames : IEnumerable {
		public IEnumerator GetEnumerator() {
			return typeof(ProjectionNamesBuilder.StandardProjections).GetFields(
					System.Reflection.BindingFlags.Public |
					System.Reflection.BindingFlags.Static |
					System.Reflection.BindingFlags.FlattenHierarchy)
				.Where(x => x.IsLiteral && !x.IsInitOnly)
				.Select(x => x.GetRawConstantValue()).GetEnumerator();
		}
	}

	[TestFixture, TestFixtureSource(typeof(SystemProjectionNames))]
	public class when_deleting_a_system_projection : TestFixtureWithProjectionCoreAndManagementServices {
		private string _systemProjectionName;

		public when_deleting_a_system_projection(string projectionName) {
			_systemProjectionName = projectionName;
		}

		protected override bool GivenInitializeSystemProjections() {
			return true;
		}

		protected override void Given() {
			AllWritesSucceed();
			NoOtherStreams();
		}

		protected override IEnumerable<WhenStep> When() {
			yield return new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid());
			yield return
				new ProjectionManagementMessage.Command.Disable(
					new PublishEnvelope(_bus), _systemProjectionName, ProjectionManagementMessage.RunAs.System);
			yield return
				new ProjectionManagementMessage.Command.Delete(
					new PublishEnvelope(_bus), _systemProjectionName,
					ProjectionManagementMessage.RunAs.System, false, false, false);
		}

		[Test, Category("v8")]
		public void a_projection_deleted_event_is_not_written() {
			Assert.IsFalse(
				_consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Any(x =>
					x.Events[0].EventType == ProjectionEventTypes.ProjectionDeleted &&
					Helper.UTF8NoBom.GetString(x.Events[0].Data) == _systemProjectionName));
		}
	}
}
