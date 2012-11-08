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
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.heading_distribution_point
{
    [TestFixture]
    public class when_starting_a_heading_distribution_point : TestFixtureWithReadWriteDisaptchers
    {
        private HeadingEventDistributionPoint _point;
        private Exception _exception;
        private Guid _distibutionPointCorrelationId;

        [SetUp]
        public void setup()
        {
            _exception = null;
            try
            {
                _point = new HeadingEventDistributionPoint(10);
            }
            catch (Exception ex)
            {
                _exception = ex;
            }

            _distibutionPointCorrelationId = Guid.NewGuid();
            _point.Start(
                _distibutionPointCorrelationId,
                new TransactionFileReaderEventDistributionPoint(
                    _bus, _distibutionPointCorrelationId, new EventPosition(0, -1)));
        }


        [Test]
        public void transaction_file_reader_publishes_read_events_from_tf()
        {
            Assert.IsTrue(_consumer.HandledMessages.OfType<ClientMessage.ReadAllEventsForward>().Any());
        }

        [Test]
        public void can_handle_events()
        {
            _point.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    _distibutionPointCorrelationId, new EventPosition(20, 10), "stream", 10, false,
                    new Event(Guid.NewGuid(), "type", false, new byte[0], new byte[0])));
        }

        [Test]
        public void can_be_stopped()
        {
            _point.Stop();
        }


        [Test]
        public void cannot_suibscribe_even_from_reader_zero_position()
        {
            var subscribed = _point.TrySubscribe(
                Guid.NewGuid(), new FakeProjectionSubscription(), -1);
            Assert.AreEqual(false, subscribed);
        }
    }
}
