using System;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection {
	[TestFixture]
	public class when_loading_a_managed_projection_state : TestFixtureWithExistingEvents {
		private new ITimeProvider _timeProvider;

		private ManagedProjection _mp;

		protected override void Given() {
			_timeProvider = new FakeTimeProvider();
			_mp = new ManagedProjection(
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

		[Test]
		public void null_handler_type_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => {
				ProjectionManagementMessage.Command.Post message = new ProjectionManagementMessage.Command.Post(
					new NoopEnvelope(), ProjectionMode.OneTime, "name", ProjectionManagementMessage.RunAs.Anonymous,
					(string)null, @"log(1);", enabled: true, checkpointsEnabled: false, emitEnabled: false,
					trackEmittedStreams: false);
				_mp.InitializeNew(
					new ManagedProjection.PersistedState {
						Enabled = message.Enabled,
						HandlerType = message.HandlerType,
						Query = message.Query,
						Mode = message.Mode,
						EmitEnabled = message.EmitEnabled,
						CheckpointsDisabled = !message.CheckpointsEnabled,
						Epoch = -1,
						Version = -1,
						RunAs = message.EnableRunAs ? SerializedRunAs.SerializePrincipal(message.RunAs) : null,
					},
					null);
			});
		}

		[Test]
		public void empty_handler_type_throws_argument_null_exception() {
			Assert.Throws<ArgumentException>(() => {
				ProjectionManagementMessage.Command.Post message = new ProjectionManagementMessage.Command.Post(
					new NoopEnvelope(), ProjectionMode.OneTime, "name", ProjectionManagementMessage.RunAs.Anonymous, "",
					@"log(1);", enabled: true, checkpointsEnabled: false, emitEnabled: false,
					trackEmittedStreams: false);
				_mp.InitializeNew(
					new ManagedProjection.PersistedState {
						Enabled = message.Enabled,
						HandlerType = message.HandlerType,
						Query = message.Query,
						Mode = message.Mode,
						EmitEnabled = message.EmitEnabled,
						CheckpointsDisabled = !message.CheckpointsEnabled,
						Epoch = -1,
						Version = -1,
						RunAs = message.EnableRunAs ? SerializedRunAs.SerializePrincipal(message.RunAs) : null,
					},
					null);
			});
		}

		[Test]
		public void null_query_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => {
				ProjectionManagementMessage.Command.Post message = new ProjectionManagementMessage.Command.Post(
					new NoopEnvelope(), ProjectionMode.OneTime, "name", ProjectionManagementMessage.RunAs.Anonymous,
					"JS", query: null, enabled: true, checkpointsEnabled: false, emitEnabled: false,
					trackEmittedStreams: false);
				_mp.InitializeNew(
					new ManagedProjection.PersistedState {
						Enabled = message.Enabled,
						HandlerType = message.HandlerType,
						Query = message.Query,
						Mode = message.Mode,
						EmitEnabled = message.EmitEnabled,
						CheckpointsDisabled = !message.CheckpointsEnabled,
						Epoch = -1,
						Version = -1,
						RunAs = message.EnableRunAs ? SerializedRunAs.SerializePrincipal(message.RunAs) : null,
					},
					null);
			});
		}
	}
}
