using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	[TestFixture]
	public class
		when_receiving_create_and_prepare_slave_command :
			specification_with_projection_core_service_command_reader_started {
		private const string Query = @"fromStream('$user-admin').outputState()";
		private Guid _projectionId;

		protected override IEnumerable<WhenStep> When() {
			_projectionId = Guid.NewGuid();
			yield return
				CreateWriteEvent(
					"$projections-$" + _serviceId,
					"$create-and-prepare-slave",
					@"{
                        ""id"":""" + _projectionId.ToString("N") + @""",
                          ""config"":{
                             ""runAs"":""user"",
                             ""runAsRoles"":[""a"",""b""],
                             ""checkpointHandledThreshold"":1000,
                             ""checkpointUnhandledBytesThreshold"":10000,
                             ""pendingEventsThreshold"":5000, 
                             ""maxWriteBatchLength"":100,
                             ""maximumAllowedWritesInFlight"":1,
                             ""emitEventEnabled"":true,
                             ""checkpointsEnabled"":true,
                             ""createTempStreams"":true,
                             ""stopOnEof"":true,
                             ""isSlaveProjection"":true,
                         },
                         ""version"":{},
                         ""handlerType"":""JS"",
                         ""query"":""" + Query + @""",
                         ""name"":""test"",
                         ""masterWorkerId"":""2251f64ac0a24c599414d7fe2ada13b1"",
                         ""masterCoreProjectionId"":""85032c4bc58546b4be116f5c4b305bb6"",
                    }",
					null,
					true);
		}

		[Test]
		public void publishes_projection_create_prepapre_slave_message() {
			var createPrepareSlave =
				HandledMessages.OfType<CoreProjectionManagementMessage.CreateAndPrepareSlave>().LastOrDefault();
			Assert.IsNotNull(createPrepareSlave);
			Assert.AreEqual(_projectionId, createPrepareSlave.ProjectionId);
			Assert.AreEqual("JS", createPrepareSlave.HandlerType);
			Assert.AreEqual(Query, createPrepareSlave.Query);
			Assert.AreEqual("test", createPrepareSlave.Name);
			Assert.IsNotNull(createPrepareSlave.Config);
			Assert.AreEqual("user", createPrepareSlave.Config.RunAs.Identity.Name);
			Assert.That(createPrepareSlave.Config.RunAs.IsInRole("b"));
			Assert.AreEqual(1000, createPrepareSlave.Config.CheckpointHandledThreshold);
			Assert.AreEqual(10000, createPrepareSlave.Config.CheckpointUnhandledBytesThreshold);
			Assert.AreEqual(5000, createPrepareSlave.Config.PendingEventsThreshold);
			Assert.AreEqual(100, createPrepareSlave.Config.MaxWriteBatchLength);
			Assert.AreEqual(1, createPrepareSlave.Config.MaximumAllowedWritesInFlight);
			Assert.AreEqual(true, createPrepareSlave.Config.EmitEventEnabled);
			Assert.AreEqual(true, createPrepareSlave.Config.CheckpointsEnabled);
			Assert.AreEqual(true, createPrepareSlave.Config.CreateTempStreams);
			Assert.AreEqual(true, createPrepareSlave.Config.StopOnEof);
			Assert.AreEqual(true, createPrepareSlave.Config.IsSlaveProjection);
		}
	}
}
