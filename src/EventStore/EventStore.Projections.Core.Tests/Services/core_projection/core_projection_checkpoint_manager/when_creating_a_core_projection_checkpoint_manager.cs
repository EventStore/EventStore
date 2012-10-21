// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.core_projection_checkpoint_manager
{
    [TestFixture]
    public class when_creating_a_core_projection_checkpoint_manager : TestFixtureWithCoreProjectionCheckpointManager
    {
        protected override void When()
        {
            // do not create
        }

        [Test]
        public void it_can_be_created()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, _bus, _projectionCorrelationId, _readDispatcher, _writeDispatcher, _config, "$projections-projection-checkpoint", "projection",
                new StreamPositionTagger("stream"));
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_projection_throws_argument_null_exception()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                null, _bus, _projectionCorrelationId, _readDispatcher, _writeDispatcher, _config, "$projections-projection-checkpoint", "projection",
                new StreamPositionTagger("stream"));
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_publisher_throws_argument_null_exception()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, null, _projectionCorrelationId, _readDispatcher, _writeDispatcher, _config, "$projections-projection-checkpoint", "projection",
                new StreamPositionTagger("stream"));
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void null_read_dispatcher_throws_argument_null_exception()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, _bus, _projectionCorrelationId, null, _writeDispatcher, _config, "$projections-projection-checkpoint", "projection",
                new StreamPositionTagger("stream"));
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void null_write_dispatcher_throws_argument_null_exception()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, _bus, _projectionCorrelationId, _readDispatcher, null, _config, "$projections-projection-checkpoint", "projection",
                new StreamPositionTagger("stream"));
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_projection_config_throws_argument_null_exception()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, _bus, _projectionCorrelationId, _readDispatcher, _writeDispatcher, null, "$projections-projection-checkpoint", "projection",
                new StreamPositionTagger("stream"));
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_projection_checkpoint_stream_id_throws_argument_null_exception()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, _bus, _projectionCorrelationId, _readDispatcher, _writeDispatcher, _config, null, "projection",
                new StreamPositionTagger("stream"));
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_projection_name_throws_argument_null_exception()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, _bus, _projectionCorrelationId, _readDispatcher, _writeDispatcher, _config, "$projections-projection-checkpoint", null,
                new StreamPositionTagger("stream"));
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_position_tagger_throws_argument_null_exception()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, _bus, _projectionCorrelationId, _readDispatcher, _writeDispatcher, _config, "$projections-projection-checkpoint", "projection",
                null);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_projection_checkpoint_stream_id_throws_argument_exception()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, _bus, _projectionCorrelationId, _readDispatcher, _writeDispatcher, _config, "$projections-projection-checkpoint", "",
                new StreamPositionTagger("stream"));
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_projection_name_throws_argument_exception()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, _bus, _projectionCorrelationId, _readDispatcher, _writeDispatcher, _config, "$projections-projection-checkpoint", "",
                new StreamPositionTagger("stream"));
        }
    }
}
