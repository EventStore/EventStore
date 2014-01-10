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
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.core_projection.parallel_query
{
    public abstract class specification_with_parallel_query : TestFixtureWithCoreProjectionStarted
    {
        protected Guid _eventId;
        protected Guid _slave1;
        protected Guid _slave2;

        private SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;

        protected override bool GivenCheckpointsEnabled()
        {
            return true;
        }

        protected override bool GivenEmitEventEnabled()
        {
            return false;
        }

        protected override bool GivenStopOnEof()
        {
            return true;
        }

        protected override int GivenPendingEventsThreshold()
        {
            return 20;
        }

        protected override ProjectionProcessingStrategy GivenProjectionProcessingStrategy()
        {
            var sourceDefinition = _stateHandler.GetSourceDefinition();
            var source = QuerySourcesDefinition.From(sourceDefinition).ToJson();
            return new ParallelQueryProcessingStrategy(
                _projectionName, _version, GivenProjectionStateHandler(), GivenProjectionStateHandler, _projectionConfig,
                ProjectionSourceDefinition.From(
                    _projectionName, sourceDefinition, typeof (FakeProjectionStateHandler).GetNativeHandlerName(),
                    source), new ProjectionNamesBuilder(_projectionName, sourceDefinition), null,
                _spoolProcessingResponseDispatcher, _subscriptionDispatcher);
        }

        protected override void Given()
        {
            _spoolProcessingResponseDispatcher = new SpooledStreamReadingDispatcher(GetInputQueue());

            _bus.Subscribe(_spoolProcessingResponseDispatcher.CreateSubscriber<PartitionProcessingResult>());

            _slave1 = Guid.NewGuid();
            _slave2 = Guid.NewGuid();

            _checkpointHandledThreshold = 10;
            _checkpointUnhandledBytesThreshold = 10000;

            _configureBuilderByQuerySource = source =>
            {
                source.FromCatalogStream("catalog");
                source.AllEvents();
                source.SetOutputState();
                source.SetByStream();
            };
            _slaveProjections =
                new SlaveProjectionCommunicationChannels(
                    new Dictionary<string, SlaveProjectionCommunicationChannel[]>
                    {
                        {
                            "slave",
                            new[]
                            {
                                new SlaveProjectionCommunicationChannel("s1", Guid.Empty, _slave1, GetInputQueue()),
                                new SlaveProjectionCommunicationChannel("s2", Guid.Empty, _slave2, GetInputQueue())
                            }
                        }
                    });

            TicksAreHandledImmediately();
            AllWritesSucceed();
            NoOtherStreams();
        }

        protected override FakeProjectionStateHandler GivenProjectionStateHandler()
        {
            return new FakeProjectionStateHandler(
                configureBuilder: _configureBuilderByQuerySource, failOnGetPartition: false);
        }
    }
}
