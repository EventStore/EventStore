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
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    [TestFixture]
    public class when_creating : TestFixtureWithExistingEvents
    {
        private string[] _abStreams;
        private Dictionary<string, int> _ab12Tag;
        private RealTimeProvider _timeProvider;

        [SetUp]
        public void setup()
        {
            _timeProvider = new RealTimeProvider();
            _ab12Tag = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
            _abStreams = new[] {"a", "b"};
        }

        [Test]
        public void it_can_be_created()
        {
            var edp = new MultiStreamEventReader(_bus, Guid.NewGuid(), null, _abStreams, _ab12Tag, false, _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_publisher_throws_argument_null_exception()
        {
            var edp = new MultiStreamEventReader(null, Guid.NewGuid(), null, _abStreams, _ab12Tag, false, _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_event_reader_id_throws_argument_exception()
        {
            var edp = new MultiStreamEventReader(_bus, Guid.Empty, null, _abStreams, _ab12Tag, false, _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_streams_throws_argument_null_exception()
        {
            var edp = new MultiStreamEventReader(_bus, Guid.NewGuid(), null, null, _ab12Tag, false, _timeProvider);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void null_time_provider_throws_argument_null_exception()
        {
            var edp = new MultiStreamEventReader(_bus, Guid.NewGuid(), null, _abStreams, _ab12Tag, false, null);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void empty_streams_throws_argument_exception()
        {
            var edp = new MultiStreamEventReader(
                _bus, Guid.NewGuid(), null, new string[0], _ab12Tag, false, _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void invalid_from_tag_throws_argument_exception()
        {
            var edp = new MultiStreamEventReader(
                _bus, Guid.NewGuid(), null, _abStreams, new Dictionary<string, int> {{"a", 1}, {"c", 2}}, false,
                _timeProvider);
        }
    }
}
