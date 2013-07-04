﻿// Copyright (c) 2012, Event Store LLP
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
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.stream_reader
{
    [TestFixture]
    public class when_creating_stream_event_reader : TestFixtureWithExistingEvents
    {
        [Test]
        public void it_can_be_created()
        {
            new StreamEventReader(_bus, Guid.NewGuid(), null, "stream", 0, new RealTimeProvider(), false);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_publisher_throws_argument_null_exception()
        {
            new StreamEventReader(null, Guid.NewGuid(), null, "stream", 0, new RealTimeProvider(), false);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void empty_event_reader_id_throws_argument_exception()
        {
            new StreamEventReader(_bus, Guid.Empty, null, "stream", 0, new RealTimeProvider(), false);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_stream_name_throws_argument_null_exception()
        {
            new StreamEventReader(_bus, Guid.NewGuid(), null, null, 0, new RealTimeProvider(), false);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_stream_name_throws_argument_exception()
        {
            new StreamEventReader(_bus, Guid.NewGuid(), null, "", 0, new RealTimeProvider(), false);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void negative_event_sequence_number_throws_argument_exception()
        {
            new StreamEventReader(_bus, Guid.NewGuid(), null, "", -1, new RealTimeProvider(), false);
        }
    }
}
