using System;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;
using TestFixtureWithReadWriteDispatchers =
	EventStore.Projections.Core.Tests.Services.core_projection.TestFixtureWithReadWriteDispatchers;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection {
	[TestFixture]
	public class when_creating_a_managed_projection : TestFixtureWithReadWriteDispatchers {
		private new ITimeProvider _timeProvider;

		private
			RequestResponseDispatcher<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>
			_getStateDispatcher;

		private
			RequestResponseDispatcher
			<CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>
			_getResultDispatcher;

		[SetUp]
		public void setup() {
			_timeProvider = new FakeTimeProvider();
			_getStateDispatcher =
				new RequestResponseDispatcher
					<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>(
						_bus,
						v => v.CorrelationId,
						v => v.CorrelationId,
						new PublishEnvelope(_bus));
			_getResultDispatcher =
				new RequestResponseDispatcher
					<CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>(
						_bus,
						v => v.CorrelationId,
						v => v.CorrelationId,
						new PublishEnvelope(_bus));
		}


		[Test]
		public void empty_guid_throws_invali_argument_exception() {
			Assert.Throws<ArgumentException>(() => {
				new ManagedProjection(
					Guid.NewGuid(),
					Guid.Empty,
					1,
					"name",
					true,
					null,
					_streamDispatcher,
					_writeDispatcher,
					_readDispatcher,
					_bus,
					_timeProvider,
					_getStateDispatcher,
					_getResultDispatcher,
					_ioDispatcher,
					TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault));
			});
		}

		[Test]
		public void empty_guid_throws_invali_argument_exception2() {
			Assert.Throws<ArgumentException>(() => {
				new ManagedProjection(
					Guid.NewGuid(),
					Guid.Empty,
					1,
					"name",
					true,
					null,
					_streamDispatcher,
					_writeDispatcher,
					_readDispatcher,
					_bus,
					_timeProvider,
					new RequestResponseDispatcher
						<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>(
							_bus,
							v => v.CorrelationId,
							v => v.CorrelationId,
							new PublishEnvelope(_bus)),
					new RequestResponseDispatcher
						<CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>(
							_bus,
							v => v.CorrelationId,
							v => v.CorrelationId,
							new PublishEnvelope(_bus)),
					_ioDispatcher,
					TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault));
			});
		}

		[Test]
		public void null_name_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => {
				new ManagedProjection(
					Guid.NewGuid(),
					Guid.NewGuid(),
					1,
					null,
					true,
					null,
					_streamDispatcher,
					_writeDispatcher,
					_readDispatcher,
					_bus,
					_timeProvider,
					new RequestResponseDispatcher
						<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>(
							_bus,
							v => v.CorrelationId,
							v => v.CorrelationId,
							new PublishEnvelope(_bus)),
					new RequestResponseDispatcher
						<CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>(
							_bus,
							v => v.CorrelationId,
							v => v.CorrelationId,
							new PublishEnvelope(_bus)),
					_ioDispatcher,
					TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault));
			});
		}

		[Test]
		public void null_name_throws_argument_null_exception2() {
			Assert.Throws<ArgumentNullException>(() => {
				new ManagedProjection(
					Guid.NewGuid(),
					Guid.NewGuid(),
					1,
					null,
					true,
					null,
					_streamDispatcher,
					_writeDispatcher,
					_readDispatcher,
					_bus,
					_timeProvider,
					new RequestResponseDispatcher
						<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>(
							_bus,
							v => v.CorrelationId,
							v => v.CorrelationId,
							new PublishEnvelope(_bus)),
					new RequestResponseDispatcher
						<CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>(
							_bus,
							v => v.CorrelationId,
							v => v.CorrelationId,
							new PublishEnvelope(_bus)),
					_ioDispatcher,
					TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault));
			});
		}

		[Test]
		public void empty_name_throws_argument_exception() {
			Assert.Throws<ArgumentException>(() => {
				new ManagedProjection(
					Guid.NewGuid(),
					Guid.NewGuid(),
					1,
					"",
					true,
					null,
					_streamDispatcher,
					_writeDispatcher,
					_readDispatcher,
					_bus,
					_timeProvider,
					new RequestResponseDispatcher
						<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>(
							_bus,
							v => v.CorrelationId,
							v => v.CorrelationId,
							new PublishEnvelope(_bus)),
					new RequestResponseDispatcher
						<CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>(
							_bus,
							v => v.CorrelationId,
							v => v.CorrelationId,
							new PublishEnvelope(_bus)),
					_ioDispatcher,
					TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault));
			});
		}

		[Test]
		public void empty_name_throws_argument_exception2() {
			Assert.Throws<ArgumentException>(() => {
				new ManagedProjection(
					Guid.NewGuid(),
					Guid.NewGuid(),
					1,
					"",
					true,
					null,
					_streamDispatcher,
					_writeDispatcher,
					_readDispatcher,
					_bus,
					_timeProvider,
					new RequestResponseDispatcher
						<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>(
							_bus,
							v => v.CorrelationId,
							v => v.CorrelationId,
							new PublishEnvelope(_bus)),
					new RequestResponseDispatcher
						<CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>(
							_bus,
							v => v.CorrelationId,
							v => v.CorrelationId,
							new PublishEnvelope(_bus)),
					_ioDispatcher,
					TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault));
			});
		}
	}
}
