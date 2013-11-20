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
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using System.Linq;

namespace EventStore.Projections.Core.Tests.Services.projections_system
{
    public abstract class with_projections_subsystem : TestFixtureWithProjectionCoreAndManagementServices
    {
        protected bool _startSystemProjections;

        protected override bool GivenInitializeSystemProjections()
        {
            return true;
        }

        protected override void Given1()
        {
            base.Given1();
            _startSystemProjections = GivenStartSystemProjections();
            AllWritesSucceed();
            NoOtherStreams();
            EnableReadAll();
        }

        protected virtual bool GivenStartSystemProjections()
        {
            return false;
        }

        protected override IEnumerable<WhenStep> PreWhen()
        {
            yield return (new SystemMessage.BecomeMaster(Guid.NewGuid()));
            yield return Yield;
            if (_startSystemProjections)
            {
                yield return
                    new ProjectionManagementMessage.GetStatistics(Envelope, ProjectionMode.AllNonTransient, null, false)
                    ;
                var statistics = HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Last();
                foreach (var projection in statistics.Projections)
                {
                    if (projection.Status != "Running")
                        yield return
                            new ProjectionManagementMessage.Enable(
                                Envelope, projection.Name, ProjectionManagementMessage.RunAs.Anonymous);
                }
            }
        }
    }
}
