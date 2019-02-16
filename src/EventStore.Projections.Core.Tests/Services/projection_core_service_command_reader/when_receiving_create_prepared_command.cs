using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	[TestFixture]
	public class
		when_receiving_create_prepared_command : specification_with_projection_core_service_command_reader_started {
		private const string Query = @"fromStream('$user-admin').outputState()";
		private Guid _projectionId;

		protected override IEnumerable<WhenStep> When() {
			_projectionId = Guid.NewGuid();
			yield return
				CreateWriteEvent(
					"$projections-$" + _serviceId,
					"$create-prepared",
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
                         ""sourceDefinition"":{
                             ""allEvents"":false,   
                             ""allStreams"":false,
                             ""byStreams"":true,
                             ""byCustomPartitions"":false,
                             ""categories"":[""account""],
                             ""events"":[""added"",""removed""],
                             ""streams"":[],
                             ""catalogStream"":"""",
                             ""limitingCommitPosition"":100000,
                             ""options"":{
                                 ""resultStreamName"":""ResultStreamName"",
                                 ""partitionResultStreamNamePattern"":""PartitionResultStreamNamePattern"",
                                 ""reorderEvents"":false,
                                 ""processingLag"":0,
                                 ""isBiState"":false,
                                 ""definesStateTransform"":false,
                                 ""definesCatalogTransform"":false,
                                 ""producesResults"":true,
                                 ""definesFold"":false,
                                 ""handlesDeletedNotifications"":false,
                                 ""$includeLinks"":false,
                                 ""disableParallelism"":false,
                             },
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
		public void publishes_projection_create_prepapred_message() {
			var createPrepared =
				HandledMessages.OfType<CoreProjectionManagementMessage.CreatePrepared>().LastOrDefault();
			Assert.IsNotNull(createPrepared);
			Assert.AreEqual(_projectionId, createPrepared.ProjectionId);
			Assert.AreEqual("JS", createPrepared.HandlerType);
			Assert.AreEqual(Query, createPrepared.Query);
			Assert.AreEqual("test", createPrepared.Name);
			Assert.IsNotNull(createPrepared.Config);
			Assert.AreEqual("user", createPrepared.Config.RunAs.Identity.Name);
			Assert.That(createPrepared.Config.RunAs.IsInRole("b"));
			Assert.AreEqual(1000, createPrepared.Config.CheckpointHandledThreshold);
			Assert.AreEqual(10000, createPrepared.Config.CheckpointUnhandledBytesThreshold);
			Assert.AreEqual(5000, createPrepared.Config.PendingEventsThreshold);
			Assert.AreEqual(100, createPrepared.Config.MaxWriteBatchLength);
			Assert.AreEqual(1, createPrepared.Config.MaximumAllowedWritesInFlight);
			Assert.AreEqual(true, createPrepared.Config.EmitEventEnabled);
			Assert.AreEqual(true, createPrepared.Config.CheckpointsEnabled);
			Assert.AreEqual(true, createPrepared.Config.CreateTempStreams);
			Assert.AreEqual(false, createPrepared.Config.StopOnEof);
			Assert.AreEqual(false, createPrepared.Config.IsSlaveProjection);
			var projectionSourceDefinition = createPrepared.SourceDefinition as IQuerySources;
			Assert.IsNotNull(projectionSourceDefinition);
			Assert.AreEqual(false, projectionSourceDefinition.AllEvents);
			Assert.AreEqual(false, projectionSourceDefinition.AllStreams);
			Assert.AreEqual(true, projectionSourceDefinition.ByStreams);
			Assert.AreEqual(false, projectionSourceDefinition.ByCustomPartitions);
			Assert.That(new[] {"account"}.SequenceEqual(projectionSourceDefinition.Categories));
			Assert.That(new[] {"added", "removed"}.SequenceEqual(projectionSourceDefinition.Events));
			Assert.That(new string[] { }.SequenceEqual(projectionSourceDefinition.Streams));
			Assert.AreEqual("", projectionSourceDefinition.CatalogStream);
			Assert.AreEqual(100000, projectionSourceDefinition.LimitingCommitPosition);
			Assert.AreEqual("ResultStreamName", projectionSourceDefinition.ResultStreamNameOption);
			Assert.AreEqual(
				"PartitionResultStreamNamePattern",
				projectionSourceDefinition.PartitionResultStreamNamePatternOption);
			Assert.AreEqual(false, projectionSourceDefinition.ReorderEventsOption);
			Assert.AreEqual(0, projectionSourceDefinition.ProcessingLagOption);
			Assert.AreEqual(false, projectionSourceDefinition.IsBiState);
			Assert.AreEqual(false, projectionSourceDefinition.DefinesStateTransform);
			Assert.AreEqual(false, projectionSourceDefinition.DefinesCatalogTransform);
			Assert.AreEqual(true, projectionSourceDefinition.ProducesResults);
			Assert.AreEqual(false, projectionSourceDefinition.DefinesFold);
			Assert.AreEqual(false, projectionSourceDefinition.HandlesDeletedNotifications);
			Assert.AreEqual(false, projectionSourceDefinition.IncludeLinksOption);
			Assert.AreEqual(false, projectionSourceDefinition.DisableParallelismOption);
		}
	}
}
