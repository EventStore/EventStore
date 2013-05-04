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
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.onetime
{
    public class an_expired_projection
    {
        public abstract class Base : a_new_posted_projection.Base
        {
            protected Guid _reader;

            protected override void Given()
            {
                base.Given();
            }

            protected override void When()
            {
                base.When();
                var readerAssignedMessage =
                    _consumer.HandledMessages.OfType<ReaderSubscriptionManagement.ReaderAssignedReader>().LastOrDefault();
                Assert.IsNotNull(readerAssignedMessage);
                _reader = readerAssignedMessage.ReaderId;

                _bus.Publish(
                    ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                        _reader, new TFPos(100, 50), "stream", 1, "stream", 1, false, Guid.NewGuid(), "type",
                        false, new byte[0], new byte[0], 100, 33.3f));
                _timeProvider.AddTime(TimeSpan.FromMinutes(6));
                foreach (var m in _consumer.HandledMessages.OfType<TimerMessage.Schedule>().ToArray())
                    m.Envelope.ReplyWith(m.ReplyMessage);
            }
        }

        [TestFixture]
        public class when_retrieving_statistics : Base
        {
            protected override void When()
            {
                base.When();
                _manager.Handle(
                    new ProjectionManagementMessage.GetStatistics(
                        new PublishEnvelope(_bus), null, _projectionName, false));
            }

            [Test]
            public void projection_is_not_found()
            {
                Assert.IsTrue(_consumer.HandledMessages.OfType<ProjectionManagementMessage.NotFound>().Any());
            }
        }
    }
}
