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
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.stream_event_distribution_point
{
    [TestFixture]
    public class when_creating_stream_event_distribution_point : TestFixtureWithExistingEvents
    {
        [Test]
        public void it_can_be_created()
        {
            var edp = new StreamReaderEventDistributionPoint(_bus, Guid.NewGuid(), "stream", 0, false);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_publisher_throws_argument_null_exception()
        {
            var edp = new StreamReaderEventDistributionPoint(null, Guid.NewGuid(), "stream", 0, false);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_distribution_point_id_throws_argument_exception()
        {
            var edp = new StreamReaderEventDistributionPoint(_bus, Guid.Empty, "stream", 0, false);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_stream_name_throws_argument_null_exception()
        {
            var edp = new StreamReaderEventDistributionPoint(_bus, Guid.NewGuid(), null, 0, false);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_stream_name_throws_argument_exception()
        {
            var edp = new StreamReaderEventDistributionPoint(_bus, Guid.NewGuid(), "", 0, false);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void negative_event_sequence_number_throws_argument_exception()
        {
            var edp = new StreamReaderEventDistributionPoint(_bus, Guid.NewGuid(), "", -1, false);
        }
    }
}
