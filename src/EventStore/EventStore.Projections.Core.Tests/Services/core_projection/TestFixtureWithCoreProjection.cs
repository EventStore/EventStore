using System;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    public abstract class TestFixtureWithCoreProjection : TestFixtureWithExistingEvents
    {
        protected CoreProjection _coreProjection;
        protected TestHandler<ProjectionSubscriptionManagement.Subscribe> _subscribeProjectionHandler;
        protected TestHandler<ClientMessage.WriteEvents> _writeEventHandler;
        protected readonly string _lastSeenEvent = Guid.NewGuid().ToString("D");
        protected Guid _firstWriteCorrelationId;
        protected FakeProjectionStateHandler _stateHandler;
        protected int _checkpointHandledThreshold = 5;
        protected int _checkpointUnhandledBytesThreshold = 10000;
        protected Action<QuerySourceProcessingStrategyBuilder> _configureBuilderByQuerySource = null;
        private Guid _projectionCorrelationId;
        private ProjectionMode _projectionMode = ProjectionMode.Persistent;

        [SetUp]
        public void setup()
        {
            _subscribeProjectionHandler = new TestHandler<ProjectionSubscriptionManagement.Subscribe>();
            _writeEventHandler = new TestHandler<ClientMessage.WriteEvents>();
            _bus.Subscribe(_subscribeProjectionHandler);
            _bus.Subscribe(_writeEventHandler);


            _stateHandler = _stateHandler
                            ?? new FakeProjectionStateHandler(configureBuilder: _configureBuilderByQuerySource);
            _firstWriteCorrelationId = Guid.NewGuid();
            _projectionCorrelationId = Guid.NewGuid();
            _coreProjection = new CoreProjection(
                "projection", _projectionCorrelationId, _bus, _stateHandler,
                new ProjectionConfig(
                    _projectionMode, _checkpointHandledThreshold, _checkpointUnhandledBytesThreshold, 1000, 250, true, true,
                    true), _readDispatcher, _writeDispatcher);
            PreWhen();
            When();
        }

        protected virtual void PreWhen()
        {
        }

        protected abstract void When();
    }
}