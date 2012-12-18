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

using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projection_subscription;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reordering_projection_subscription
{
    public abstract class TestFixtureWithEventReorderingProjectionSubscription : TestFixtureWithProjectionSubscription
    {
        protected int _timeBetweenEvents;
        protected int _processingLagMs;

        protected override void Given()
        {
            _timeBetweenEvents = 1100;
            _processingLagMs = 500;
            base.Given();
            _source = builder =>
                {
                    builder.FromStream("a");
                    builder.FromStream("b");
                    builder.AllEvents();
                    builder.SetEmitStateUpdated();
                    builder.SetReorderEvents(true);
                    builder.SetProcessingLag(1000); // ms
                };
        }

        protected override IProjectionSubscription CreateProjectionSubscription()
        {
            return new EventReorderingProjectionSubscription(
                _projectionCorrelationId,
                CheckpointTag.FromStreamPositions(
                    new Dictionary<string, int> {{"a", ExpectedVersion.NoStream}, {"b", ExpectedVersion.NoStream}}),
                _eventHandler, _checkpointHandler, _progressHandler, _eofHandler, _checkpointStrategy,
                _checkpointUnhandledBytesThreshold, _processingLagMs);
        }
    }
}
