using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Bus;
using EventStore.Core.Tests.Fakes;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	[TestFixture]
	public class when_handling_start_slave_projections_message : specification_with_projection_management_service {
		protected FakePublisher _coreQueue1;
		protected FakePublisher _coreQueue2;
		private string _masterProjectionName;
		private SlaveProjectionDefinitions _slaveProjectionDefinitions;
		protected Guid _masterCorrelationId;
		protected Guid _masterWorkerId;

		protected override Dictionary<Guid, IPublisher> GivenCoreQueues() {
			_coreQueue1 = new FakePublisher();
			_coreQueue2 = new FakePublisher();
			return new Dictionary<Guid, IPublisher> {{Guid.NewGuid(), _coreQueue1}, {Guid.NewGuid(), _coreQueue2}};
		}

		protected override void Given() {
			base.Given();
			_masterProjectionName = "master-projection";
			_masterCorrelationId = Guid.NewGuid();
			_masterWorkerId = Guid.NewGuid();
			_slaveProjectionDefinitions =
				new SlaveProjectionDefinitions(
					new SlaveProjectionDefinitions.Definition(
						"slave", StateHandlerFactory(), "",
						SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.OnePerThread,
						ProjectionMode.Transient,
						false, false, true, false, ProjectionManagementMessage.RunAs.System));
		}

		protected override IEnumerable<WhenStep> When() {
			foreach (var m in base.When()) yield return m;

			yield return
				new ProjectionManagementMessage.Command.StartSlaveProjections(
					Envelope,
					ProjectionManagementMessage.RunAs.System,
					_masterProjectionName,
					_slaveProjectionDefinitions,
					_masterWorkerId,
					_masterCorrelationId);
		}

		private static string StateHandlerFactory() {
			var handlerType = typeof(FakeFromCatalogStreamProjection);
			return "native:" + handlerType.Namespace + "." + handlerType.Name;
		}

		[Test, Ignore("test framework.")]
		public void publishes_slave_projections_started_message() {
			var startedMessages =
				HandledMessages.OfType<ProjectionManagementMessage.SlaveProjectionsStarted>().ToArray();
			Assert.AreEqual(1, startedMessages);
		}
	}
}
