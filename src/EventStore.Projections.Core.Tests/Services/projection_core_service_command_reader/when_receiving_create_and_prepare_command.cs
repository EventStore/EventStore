using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	[TestFixture]
	public class
		when_receiving_create_and_prepare_command : specification_with_projection_core_service_command_reader_started {
		private const string Query = @"fromStream('$user-admin').outputState()";
		private Guid _projectionId;

		protected override IEnumerable<WhenStep> When() {
			_projectionId = Guid.NewGuid();
			yield return
				CreateWriteEvent(
					"$projections-$" + _serviceId,
					"$create-and-prepare",
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
                             ""stopOnEof"":false,
                             ""isSlaveProjection"":false,
                         },
                         ""version"":{},
                         ""handlerType"":""JS"",
                         ""query"":""" + Query + @""",
                         ""name"":""test""
                    }",
					null,
					true);
		}

		[Test]
		public void publishes_projection_create_prepapre_message() {
			var createPrepare =
				HandledMessages.OfType<CoreProjectionManagementMessage.CreateAndPrepare>().LastOrDefault();
			Assert.IsNotNull(createPrepare);
			Assert.AreEqual(_projectionId, createPrepare.ProjectionId);
			Assert.AreEqual("JS", createPrepare.HandlerType);
			Assert.AreEqual(Query, createPrepare.Query);
			Assert.AreEqual("test", createPrepare.Name);
			Assert.IsNotNull(createPrepare.Config);
			Assert.AreEqual("user", createPrepare.Config.RunAs.Identity.Name);
			Assert.That(createPrepare.Config.RunAs.IsInRole("b"));
			Assert.AreEqual(1000, createPrepare.Config.CheckpointHandledThreshold);
			Assert.AreEqual(10000, createPrepare.Config.CheckpointUnhandledBytesThreshold);
			Assert.AreEqual(5000, createPrepare.Config.PendingEventsThreshold);
			Assert.AreEqual(100, createPrepare.Config.MaxWriteBatchLength);
			Assert.AreEqual(1, createPrepare.Config.MaximumAllowedWritesInFlight);
			Assert.AreEqual(true, createPrepare.Config.EmitEventEnabled);
			Assert.AreEqual(true, createPrepare.Config.CheckpointsEnabled);
			Assert.AreEqual(true, createPrepare.Config.CreateTempStreams);
			Assert.AreEqual(false, createPrepare.Config.StopOnEof);
			Assert.AreEqual(false, createPrepare.Config.IsSlaveProjection);
		}
	}
}
