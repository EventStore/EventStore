using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Tests.Services.core_projection;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection {
	[TestFixture]
	public class when_getting_config : projection_config_test_base {
		private ManagedProjection _mp;
		private Guid _projectionId = Guid.NewGuid();
		private ProjectionManagementMessage.ProjectionConfig _config;

		private ManagedProjection.PersistedState _persistedState = new ManagedProjection.PersistedState {
			Enabled = true,
			HandlerType = "JS",
			Query = "fromAll().when({});",
			Mode = ProjectionMode.Continuous,
			CheckpointsDisabled = false,
			Epoch = -1,
			Version = -1,
			RunAs = SerializedRunAs.SerializePrincipal(ProjectionManagementMessage.RunAs.Anonymous),
			EmitEnabled = false,
			TrackEmittedStreams = true,
			CheckpointAfterMs = 1,
			CheckpointHandledThreshold = 2,
			CheckpointUnhandledBytesThreshold = 3,
			PendingEventsThreshold = 4,
			MaxWriteBatchLength = 5,
			MaxAllowedWritesInFlight = 6
		};

		public when_getting_config() {
			AllWritesQueueUp();
		}

		protected override void Given() {
			_timeProvider = new FakeTimeProvider();
			_mp = CreateManagedProjection();

			_mp.InitializeNew(
				_persistedState,
				null);
			_mp.Handle(new CoreProjectionStatusMessage.Prepared(_projectionId, new ProjectionSourceDefinition()));

			// Complete write of persisted state to start projection
			OneWriteCompletes();
			_config = GetProjectionConfig(_mp);
		}

		[Test]
		public void config_should_be_same_as_persisted_state() {
			Assert.IsNotNull(_config);
			Assert.AreEqual(_persistedState.EmitEnabled, _config.EmitEnabled, "EmitEnabled");
			Assert.AreEqual(_persistedState.TrackEmittedStreams, _config.TrackEmittedStreams, "TrackEmittedStreams");
			Assert.AreEqual(_persistedState.CheckpointAfterMs, _config.CheckpointAfterMs, "CheckpointAfterMs");
			Assert.AreEqual(_persistedState.CheckpointHandledThreshold, _config.CheckpointHandledThreshold,
				"CheckpointHandledThreshold");
			Assert.AreEqual(_persistedState.CheckpointUnhandledBytesThreshold,
				_config.CheckpointUnhandledBytesThreshold, "CheckpointUnhandledBytesThreshold");
			Assert.AreEqual(_persistedState.PendingEventsThreshold, _config.PendingEventsThreshold,
				"PendingEventsThreshold");
			Assert.AreEqual(_persistedState.MaxWriteBatchLength, _config.MaxWriteBatchLength, "MaxWriteBatchLength");
			Assert.AreEqual(_persistedState.MaxAllowedWritesInFlight, _config.MaxAllowedWritesInFlight,
				"MaxAllowedWritesInFlight");
		}
	}

	[TestFixture]
	public class when_updating_projection_config_of_faulted_projection : projection_config_test_base {
		private ManagedProjection _mp;
		private Guid _projectionId = Guid.NewGuid();
		private Exception _thrownException;
		private ProjectionManagementMessage.Command.UpdateConfig _updateConfig;

		public when_updating_projection_config_of_faulted_projection() {
			AllWritesQueueUp();
		}

		protected override void Given() {
			_timeProvider = new FakeTimeProvider();
			_mp = CreateManagedProjection();
			_mp.InitializeNew(
				new ManagedProjection.PersistedState {
					Enabled = false,
					HandlerType = "JS",
					Query = "fromAll().when({});",
					Mode = ProjectionMode.Continuous,
					EmitEnabled = true,
					CheckpointsDisabled = false,
					Epoch = -1,
					Version = -1,
					RunAs = SerializedRunAs.SerializePrincipal(ProjectionManagementMessage.RunAs.Anonymous),
				},
				null);

			_mp.Handle(new CoreProjectionStatusMessage.Prepared(_projectionId, new ProjectionSourceDefinition()));
			OneWriteCompletes();
			_consumer.HandledMessages.Clear();

			_mp.Handle(new CoreProjectionStatusMessage.Faulted(
				_projectionId,
				"test"));

			_updateConfig = CreateConfig();
			try {
				_mp.Handle(_updateConfig);
			} catch (Exception ex) {
				_thrownException = ex;
			}
		}

		[Test]
		public void persisted_state_is_written() {
			var writeEvents = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().ToList();
			Assert.AreEqual(1, writeEvents.Count());
			Assert.AreEqual("$projections-name", writeEvents[0].EventStreamId);
		}

		[Test]
		public void config_update_does_not_throw_exception() {
			Assert.IsNull(_thrownException);
		}

		[Test]
		public void config_is_updated() {
			var getConfigResult = GetProjectionConfig(_mp);

			Assert.IsNotNull(getConfigResult);
			Assert.AreEqual(_updateConfig.EmitEnabled, getConfigResult.EmitEnabled);
			Assert.AreEqual(_updateConfig.TrackEmittedStreams, getConfigResult.TrackEmittedStreams);
			Assert.AreEqual(_updateConfig.CheckpointAfterMs, getConfigResult.CheckpointAfterMs);
			Assert.AreEqual(_updateConfig.CheckpointHandledThreshold, getConfigResult.CheckpointHandledThreshold);
			Assert.AreEqual(_updateConfig.CheckpointUnhandledBytesThreshold,
				getConfigResult.CheckpointUnhandledBytesThreshold);
			Assert.AreEqual(_updateConfig.PendingEventsThreshold, getConfigResult.PendingEventsThreshold);
			Assert.AreEqual(_updateConfig.MaxWriteBatchLength, getConfigResult.MaxWriteBatchLength);
			Assert.AreEqual(_updateConfig.MaxAllowedWritesInFlight, getConfigResult.MaxAllowedWritesInFlight);
		}
	}

	[TestFixture]
	public class when_updating_projection_config_of_running_projection : projection_config_test_base {
		private ManagedProjection _mp;
		private Guid _projectionId = Guid.NewGuid();

		private ManagedProjection.PersistedState _persistedState = new ManagedProjection.PersistedState {
			Enabled = true,
			HandlerType = "JS",
			Query = "fromAll().when({});",
			Mode = ProjectionMode.Continuous,
			CheckpointsDisabled = false,
			Epoch = -1,
			Version = -1,
			RunAs = SerializedRunAs.SerializePrincipal(ProjectionManagementMessage.RunAs.Anonymous),
			EmitEnabled = false,
			TrackEmittedStreams = true,
			CheckpointAfterMs = 1,
			CheckpointHandledThreshold = 2,
			CheckpointUnhandledBytesThreshold = 3,
			PendingEventsThreshold = 4,
			MaxWriteBatchLength = 5,
			MaxAllowedWritesInFlight = 6
		};

		private InvalidOperationException _thrownException;

		public when_updating_projection_config_of_running_projection() {
			AllWritesQueueUp();
		}

		protected override void Given() {
			_timeProvider = new FakeTimeProvider();
			_mp = CreateManagedProjection();

			_mp.InitializeNew(
				_persistedState,
				null);
			_mp.Handle(new CoreProjectionStatusMessage.Prepared(_projectionId, new ProjectionSourceDefinition()));

			// Complete write of persisted state to start projection
			OneWriteCompletes();

			try {
				_mp.Handle(CreateConfig());
			} catch (InvalidOperationException ex) {
				_thrownException = ex;
			}
		}

		[Test]
		public void should_throw_exception_when_trying_to_update_config() {
			Assert.IsNotNull(_thrownException);
		}

		[Test]
		public void config_should_remain_unchanged() {
			var getConfigResult = GetProjectionConfig(_mp);

			Assert.IsNotNull(getConfigResult);
			Assert.AreEqual(_persistedState.EmitEnabled, getConfigResult.EmitEnabled, "EmitEnabled");
			Assert.AreEqual(_persistedState.TrackEmittedStreams, getConfigResult.TrackEmittedStreams,
				"TrackEmittedStreams");
			Assert.AreEqual(_persistedState.CheckpointAfterMs, getConfigResult.CheckpointAfterMs, "CheckpointAfterMs");
			Assert.AreEqual(_persistedState.CheckpointHandledThreshold, getConfigResult.CheckpointHandledThreshold,
				"CheckpointHandledThreshold");
			Assert.AreEqual(_persistedState.CheckpointUnhandledBytesThreshold,
				getConfigResult.CheckpointUnhandledBytesThreshold, "CheckpointUnhandledBytesThreshold");
			Assert.AreEqual(_persistedState.PendingEventsThreshold, getConfigResult.PendingEventsThreshold,
				"PendingEventsThreshold");
			Assert.AreEqual(_persistedState.MaxWriteBatchLength, getConfigResult.MaxWriteBatchLength,
				"MaxWriteBatchLength");
			Assert.AreEqual(_persistedState.MaxAllowedWritesInFlight, getConfigResult.MaxAllowedWritesInFlight,
				"MaxAllowedWritesInFlight");
		}
	}

	public class projection_config_test_base : TestFixtureWithExistingEvents {
		protected ManagedProjection CreateManagedProjection() {
			return new ManagedProjection(
				Guid.NewGuid(),
				Guid.NewGuid(),
				1,
				"name",
				true,
				null,
				_streamDispatcher,
				_writeDispatcher,
				_readDispatcher,
				_bus,
				_timeProvider, new RequestResponseDispatcher
					<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>(
						_bus,
						v => v.CorrelationId,
						v => v.CorrelationId,
						new PublishEnvelope(_bus)), new RequestResponseDispatcher
					<CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>(
						_bus,
						v => v.CorrelationId,
						v => v.CorrelationId,
						new PublishEnvelope(_bus)),
				_ioDispatcher,
				TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault));
		}

		protected ProjectionManagementMessage.Command.UpdateConfig CreateConfig() {
			return new ProjectionManagementMessage.Command.UpdateConfig(
				new NoopEnvelope(), "name", true, false, 100, 200, 300, 400, 500, 600,
				ProjectionManagementMessage.RunAs.Anonymous);
		}

		protected ProjectionManagementMessage.ProjectionConfig GetProjectionConfig(ManagedProjection mp) {
			ProjectionManagementMessage.ProjectionConfig getConfigResult = null;
			mp.Handle(new ProjectionManagementMessage.Command.GetConfig(
				new CallbackEnvelope(m => getConfigResult = (ProjectionManagementMessage.ProjectionConfig)m), "name",
				SerializedRunAs.SerializePrincipal(ProjectionManagementMessage.RunAs.Anonymous)));
			return getConfigResult;
		}
	}
}
