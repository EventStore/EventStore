using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader
{
    [TestFixture]
    public class when_receiving_create_prepared_command : specification_with_projection_core_service_command_reader_started
    {
        private const string Query = @"fromStream('$user-admin').outputState()";
        private Guid _projectionId;

        protected override IEnumerable<WhenStep> When()
        {
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
                             ""emitEventEnabled"":true,
                             ""checkpointsEnabled"":true,
                             ""createTempStreams"":true,
                             ""stopOnEof"":false,
                             ""isSlaveProjection"":false,
                         },
                         ""sourceDefinition"":{
                             ""allEvents"":false,   
                             ""allStreams"":false,
                             ""byStream"":true,
                             ""byCustomPartitions"":false,
                             ""categories"":[""account""],
                             ""events"":[""added"",""removed""],
                             ""streams"":[],
                             ""catalogStream"":"""",
                             ""limitingCommitPosition"":100000,
                             ""options"":{
                                 ""resultStreamName"":""ResultStreamName"",
                                 ""partitionResultStreamNamePattern"":""PartitionResultStreamNamePattern"",
                                 ""forceProjectionName"":""ForceProjectionName"",
                                 ""reorderEvents"":false,
                                 ""processingLag"":0,
                                 ""isBiState"":false,
                                 ""definesStateTransform"":false,
                                 ""definesCatalogTransform"":false,
                                 ""producesResults"":true,
                                 ""definesFold"":false,
                                 ""handlesDeletedNotifications"":false,
                                 ""includeLinks"":false,
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
        public void publishes_projection_create_prepapred_message()
        {
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
            Assert.AreEqual(true, createPrepared.Config.EmitEventEnabled);
            Assert.AreEqual(true, createPrepared.Config.CheckpointsEnabled);
            Assert.AreEqual(true, createPrepared.Config.CreateTempStreams);
            Assert.AreEqual(false, createPrepared.Config.StopOnEof);
            Assert.AreEqual(false, createPrepared.Config.IsSlaveProjection);
            Assert.IsNotNull(createPrepared.SourceDefinition);
            Assert.AreEqual(false, createPrepared.SourceDefinition.AllEvents);
            Assert.AreEqual(false, createPrepared.SourceDefinition.AllStreams);
            Assert.AreEqual(true, createPrepared.SourceDefinition.ByStreams);
            Assert.AreEqual(false, createPrepared.SourceDefinition.ByCustomPartitions);
            Assert.That(new[] {"account"}.SequenceEqual(createPrepared.SourceDefinition.Categories));
            Assert.That(new[] {"added", "removed"}.SequenceEqual(createPrepared.SourceDefinition.Events));
            Assert.That(new string[] {}.SequenceEqual(createPrepared.SourceDefinition.Streams));
            Assert.AreEqual("", createPrepared.SourceDefinition.CatalogStream);
            Assert.AreEqual(100000, createPrepared.SourceDefinition.LimitingCommitPosition);
            Assert.AreEqual("ResultStreamName", createPrepared.SourceDefinition.ResultStreamNameOption);
            Assert.AreEqual(
                "PartitionResultStreamNamePattern",
                createPrepared.SourceDefinition.PartitionResultStreamNamePatternOption);
            Assert.AreEqual("ForceProjectionName", createPrepared.SourceDefinition.ForceProjectionNameOption);
            Assert.AreEqual(false, createPrepared.SourceDefinition.ReorderEventsOption);
            Assert.AreEqual(0, createPrepared.SourceDefinition.ProcessingLagOption);
            Assert.AreEqual(false, createPrepared.SourceDefinition.IsBiState);
            Assert.AreEqual(false, createPrepared.SourceDefinition.DefinesStateTransform);
            Assert.AreEqual(false, createPrepared.SourceDefinition.DefinesCatalogTransform);
            Assert.AreEqual(true, createPrepared.SourceDefinition.ProducesResults);
            Assert.AreEqual(false, createPrepared.SourceDefinition.DefinesFold);
            Assert.AreEqual(false, createPrepared.SourceDefinition.HandlesDeletedNotifications);
            Assert.AreEqual(false, createPrepared.SourceDefinition.IncludeLinksOption);
            Assert.AreEqual(false, createPrepared.SourceDefinition.DisableParallelismOption);
        }
    }
}