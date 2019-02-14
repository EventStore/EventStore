using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class when_handling_prepared_message : specification_with_projection_core_service_response_writer {
		private Guid _projectionId;
		private ProjectionSourceDefinition _definition;

		protected override void Given() {
			_projectionId = Guid.NewGuid();

			var builder = new SourceDefinitionBuilder();
			builder.FromStream("s1");
			builder.FromStream("s2");
			builder.IncludeEvent("e1");
			builder.IncludeEvent("e2");
			builder.SetByStream();
			builder.SetResultStreamNameOption("result-stream");
			_definition = ProjectionSourceDefinition.From(builder);
		}

		protected override void When() {
			_sut.Handle(new CoreProjectionStatusMessage.Prepared(_projectionId, _definition));
		}

		[Test]
		public void publishes_prepared_response() {
			var command = AssertParsedSingleCommand<Prepared>("$prepared");
			Assert.AreEqual(_projectionId.ToString("N"), command.Id);
			Assert.AreEqual(_definition.AllEvents, command.SourceDefinition.AllEvents);
			Assert.AreEqual(_definition.AllStreams, command.SourceDefinition.AllStreams);
			Assert.AreEqual(_definition.ByCustomPartitions, command.SourceDefinition.ByCustomPartitions);
			Assert.AreEqual(_definition.ByStream, command.SourceDefinition.ByStream);
			Assert.AreEqual(_definition.CatalogStream, command.SourceDefinition.CatalogStream);
			Assert.AreEqual(_definition.Categories, command.SourceDefinition.Categories);
			Assert.AreEqual(_definition.Events, command.SourceDefinition.Events);
			Assert.AreEqual(_definition.LimitingCommitPosition, command.SourceDefinition.LimitingCommitPosition);
			Assert.AreEqual(_definition.Streams, command.SourceDefinition.Streams);
			Assert.AreEqual(
				_definition.Options.DefinesCatalogTransform,
				command.SourceDefinition.Options.DefinesCatalogTransform);
			Assert.AreEqual(_definition.Options.DefinesFold, command.SourceDefinition.Options.DefinesFold);
			Assert.AreEqual(
				_definition.Options.DefinesStateTransform,
				command.SourceDefinition.Options.DefinesStateTransform);
			Assert.AreEqual(_definition.Options.DisableParallelism,
				command.SourceDefinition.Options.DisableParallelism);
			Assert.AreEqual(
				_definition.Options.HandlesDeletedNotifications,
				command.SourceDefinition.Options.HandlesDeletedNotifications);
			Assert.AreEqual(_definition.Options.IncludeLinks, command.SourceDefinition.Options.IncludeLinks);
			Assert.AreEqual(_definition.Options.IsBiState, command.SourceDefinition.Options.IsBiState);
			Assert.AreEqual(
				_definition.Options.PartitionResultStreamNamePattern,
				command.SourceDefinition.Options.PartitionResultStreamNamePattern);
			Assert.AreEqual(_definition.Options.ProcessingLag, command.SourceDefinition.Options.ProcessingLag);
			Assert.AreEqual(_definition.Options.ProducesResults, command.SourceDefinition.Options.ProducesResults);
			Assert.AreEqual(_definition.Options.ReorderEvents, command.SourceDefinition.Options.ReorderEvents);
			Assert.AreEqual(_definition.Options.ResultStreamName, command.SourceDefinition.Options.ResultStreamName);
		}
	}
}
