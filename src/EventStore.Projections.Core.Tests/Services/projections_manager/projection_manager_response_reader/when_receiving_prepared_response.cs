using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.projection_manager_response_reader {
	[TestFixture]
	public class when_receiving_prepared_response : specification_with_projection_manager_response_reader_started {
		private const string Query = @"fromStream('$user-admin').outputState()";
		private Guid _projectionId;

		protected override IEnumerable<WhenStep> When() {
			_projectionId = Guid.NewGuid();
			yield return
				CreateWriteEvent(
					"$projections-$master",
					"$prepared",
					@"{
                        ""id"":""" + _projectionId.ToString("N") + @""",
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
                                 ""reorderEvents"":false,
                                 ""processingLag"":0,
                                 ""isBiState"":false,
                                 ""definesStateTransform"":false,
                                 ""definesCatalogTransform"":false,
                                 ""producesResults"":true,
                                 ""definesFold"":false,
                                 ""handlesDeletedNotifications"":false,
                                 ""includeLinks"":true,
                                 ""disableParallelism"":true,
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
		public void publishes_prepared_message() {
			var createPrepared =
				HandledMessages.OfType<CoreProjectionStatusMessage.Prepared>().LastOrDefault();
			Assert.IsNotNull(createPrepared);
			Assert.AreEqual(_projectionId, createPrepared.ProjectionId);
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
			Assert.AreEqual(true, projectionSourceDefinition.IncludeLinksOption);
			Assert.AreEqual(true, projectionSourceDefinition.DisableParallelismOption);
		}
	}
}
