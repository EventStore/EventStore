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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    [TestFixture]
    public class when_has_been_created : TestFixtureWithExistingEvents
    {
        private MultiStreamReaderEventDistributionPoint _edp;
        private Guid _publishWithCorrelationId;
        private Guid _distibutionPointCorrelationId;
        private string[] _abStreams;
        private Dictionary<string, int> _ab12Tag;

        [SetUp]
        public void When()
        {
            _ab12Tag = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
            _abStreams = new[] { "a", "b" };

            _publishWithCorrelationId = Guid.NewGuid();
            _distibutionPointCorrelationId = Guid.NewGuid();
            _edp = new MultiStreamReaderEventDistributionPoint(_bus, _distibutionPointCorrelationId, _abStreams, _ab12Tag, false, new RealTimeProvider());
        }

        [Test]
        public void it_can_be_resumed()
        {
            _edp.Resume();
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void it_cannot_be_paused()
        {
            _edp.Pause();
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void handle_read_events_completed_throws()
        {
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", new EventLinkPair[0], RangeReadResult.Success, -1, 4, false, 100));
        }
    }
}
