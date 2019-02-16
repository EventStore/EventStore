using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.core_projection.parallel_query {
	public abstract class specification_with_parallel_query : TestFixtureWithCoreProjectionStarted {
		protected Guid _eventId;
		protected Guid _masterProjectionId;
		protected Guid _slave1;
		protected Guid _slave2;

		private SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;

		protected override bool GivenCheckpointsEnabled() {
			return true;
		}

		protected override bool GivenEmitEventEnabled() {
			return false;
		}

		protected override bool GivenStopOnEof() {
			return true;
		}

		protected override int GivenPendingEventsThreshold() {
			return 20;
		}

		protected override ProjectionProcessingStrategy GivenProjectionProcessingStrategy() {
			var sourceDefinition = _stateHandler.GetSourceDefinition();
			var source = QuerySourcesDefinition.From(sourceDefinition).ToJson();
			return new ParallelQueryProcessingStrategy(
				_projectionName,
				_version,
				GivenProjectionStateHandler(),
				_projectionConfig,
				sourceDefinition,
				typeof(FakeProjectionStateHandler).GetNativeHandlerName(),
				source,
				new ProjectionNamesBuilder(_projectionName, sourceDefinition),
				null,
				_spoolProcessingResponseDispatcher,
				_subscriptionDispatcher);
		}

		protected override void Given() {
			_spoolProcessingResponseDispatcher = new SpooledStreamReadingDispatcher(GetInputQueue());

			_bus.Subscribe(_spoolProcessingResponseDispatcher.CreateSubscriber<PartitionProcessingResult>());

			_masterProjectionId = Guid.NewGuid();
			_slave1 = Guid.NewGuid();
			_slave2 = Guid.NewGuid();

			_checkpointHandledThreshold = 10;
			_checkpointUnhandledBytesThreshold = 10000;

			_configureBuilderByQuerySource = source => {
				source.FromCatalogStream("catalog");
				source.AllEvents();
				source.SetOutputState();
				source.SetByStream();
			};
			_slaveProjections =
				new SlaveProjectionCommunicationChannels(
					new Dictionary<string, SlaveProjectionCommunicationChannel[]> {
						{
							"slave",
							new[] {
								new SlaveProjectionCommunicationChannel(
									"s1",
									_workerId,
									_slave1),
								new SlaveProjectionCommunicationChannel(
									"s2",
									_workerId,
									_slave2)
							}
						}
					});

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
