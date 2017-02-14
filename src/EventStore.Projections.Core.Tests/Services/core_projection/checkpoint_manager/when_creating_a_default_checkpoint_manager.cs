﻿using System;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager
{
    [TestFixture]
    public class when_creating_a_default_checkpoint_manager : TestFixtureWithCoreProjectionCheckpointManager
    {
        private CoreProjectionCheckpointWriter _coreProjectionCheckpointWriter;

        protected override void When()
        {
            // do not create
            _coreProjectionCheckpointWriter =
                new CoreProjectionCheckpointWriter(
                    _namingBuilder.MakeCheckpointStreamName(), _ioDispatcher, _projectionVersion, _projectionName);
            _namingBuilder = ProjectionNamesBuilder.CreateForTest("projection");
        }

        [Test]
        public void it_can_be_created()
        {
            _manager = new DefaultCheckpointManager(
                _bus, _projectionCorrelationId, new ProjectionVersion(1, 0, 0), null, _ioDispatcher, _config,
                "projection", new StreamPositionTagger(0, "stream"), _namingBuilder, _checkpointsEnabled,
                _producesResults, _definesFold, _coreProjectionCheckpointWriter);
        }

        [Test]
        public void null_publisher_throws_argument_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => {
                _manager = new DefaultCheckpointManager(
                    null, _projectionCorrelationId, new ProjectionVersion(1, 0, 0), null, _ioDispatcher, _config,
                    "projection", new StreamPositionTagger(0, "stream"), _namingBuilder, _checkpointsEnabled,
                    _producesResults, _definesFold, _coreProjectionCheckpointWriter);
            });
        }

        [Test]
        public void null_io_dispatcher_throws_argument_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => {
            _manager = new DefaultCheckpointManager(
                _bus, _projectionCorrelationId, new ProjectionVersion(1, 0, 0), null, null, _config, "projection",
                new StreamPositionTagger(0, "stream"), _namingBuilder, _checkpointsEnabled, _producesResults,
                _definesFold, _coreProjectionCheckpointWriter);
            });
        }

        [Test]
        public void null_projection_config_throws_argument_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => {
            _manager = new DefaultCheckpointManager(
                _bus, _projectionCorrelationId, new ProjectionVersion(1, 0, 0), null, _ioDispatcher, null, "projection",
                new StreamPositionTagger(0, "stream"), _namingBuilder, _checkpointsEnabled, _producesResults,
                _definesFold, _coreProjectionCheckpointWriter);
            });
        }

        [Test]
        public void null_projection_name_throws_argument_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => {
            _manager = new DefaultCheckpointManager(
                _bus, _projectionCorrelationId, new ProjectionVersion(1, 0, 0), null, _ioDispatcher, _config, null,
                new StreamPositionTagger(0, "stream"), _namingBuilder, _checkpointsEnabled, _producesResults,
                _definesFold, _coreProjectionCheckpointWriter);
            });
        }

        [Test]
        public void null_position_tagger_throws_argument_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => {
            _manager = new DefaultCheckpointManager(
                _bus, _projectionCorrelationId, new ProjectionVersion(1, 0, 0), null, _ioDispatcher, _config,
                "projection", null, _namingBuilder, _checkpointsEnabled, _producesResults,
                _definesFold, _coreProjectionCheckpointWriter);
            });
        }

        [Test]
        public void empty_projection_checkpoint_stream_id_throws_argument_exception()
        {
            Assert.Throws<ArgumentException>(() => {
            _manager = new DefaultCheckpointManager(
                _bus, _projectionCorrelationId, new ProjectionVersion(1, 0, 0), null, _ioDispatcher, _config, "",
                new StreamPositionTagger(0, "stream"), _namingBuilder, _checkpointsEnabled, _producesResults,
                _definesFold, _coreProjectionCheckpointWriter);
            });
        }

        [Test]
        public void empty_projection_name_throws_argument_exception()
        {
            Assert.Throws<ArgumentException>(() => {
            _manager = new DefaultCheckpointManager(
                _bus, _projectionCorrelationId, new ProjectionVersion(1, 0, 0), null, _ioDispatcher, _config, "",
                new StreamPositionTagger(0, "stream"), _namingBuilder, _checkpointsEnabled, _producesResults,
                _definesFold, _coreProjectionCheckpointWriter);
            });
        }
    }
}
