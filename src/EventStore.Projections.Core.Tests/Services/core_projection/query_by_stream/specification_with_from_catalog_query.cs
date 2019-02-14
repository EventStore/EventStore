using System;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.core_projection.query_by_stream {
	public abstract class specification_with_from_catalog_query : TestFixtureWithCoreProjectionStarted {
		protected Guid _eventId;

		protected override bool GivenCheckpointsEnabled() {
			return false;
		}

		protected override bool GivenEmitEventEnabled() {
			return false;
		}

		protected override bool GivenStopOnEof() {
			return true;
		}

		protected override int GivenPendingEventsThreshold() {
			return 0;
		}

		protected override ProjectionProcessingStrategy GivenProjectionProcessingStrategy() {
			return CreateQueryProcessingStrategy();
		}

		protected override void Given() {
			_checkpointHandledThreshold = 0;
			_checkpointUnhandledBytesThreshold = 0;

			_configureBuilderByQuerySource = source => {
				source.FromCatalogStream("catalog");
				source.AllEvents();
				source.SetOutputState();
				source.SetByStream();
			};
			TicksAreHandledImmediately();
			AllWritesSucceed();
			NoOtherStreams();
		}

		protected override FakeProjectionStateHandler GivenProjectionStateHandler() {
			return new FakeProjectionStateHandler(
				configureBuilder: _configureBuilderByQuerySource, failOnGetPartition: false);
		}
	}
}
