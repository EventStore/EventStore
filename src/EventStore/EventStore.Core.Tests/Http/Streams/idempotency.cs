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
using System.Net;
using EventStore.Core.Tests.Helpers;
using EventStore.Transport.Http;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace EventStore.Core.Tests.Http.Streams
{
    namespace idempotency
    {
        abstract class HttpBehaviorSpecificationOfSuccessfulCreateEvent : HttpBehaviorSpecification
        {
            protected HttpWebResponse _response;
            [Test]
            public void returns_created_status_code()
            {
                Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
            }

            [Test]
            public void returns_a_location_header()
            {
                Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
            }

            [Test]
            public void returns_a_location_header_ending_with_zero()
            {
                var location = _response.Headers[HttpResponseHeader.Location];
                var tail = location.Substring(location.Length - "/0".Length);
                Assert.AreEqual("/0", tail);
            }

            [Test]
            public void returns_a_location_header_that_can_be_read_as_json()
            {
                var json = GetJson<JObject>(_response.Headers[HttpResponseHeader.Location]);
                HelperExtensions.AssertJson(new { A = "1" }, json);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_posting_an_event_twice : HttpBehaviorSpecificationOfSuccessfulCreateEvent
        {
            private Guid _eventId;

            protected override void Given()
            {
                _eventId = Guid.NewGuid();
                var response1 = MakeJsonPost(
                    TestStream,
                    new[] { new { EventId = _eventId, EventType = "event-type", Data = new { A = "1" } } });
                Assert.AreEqual(HttpStatusCode.Created, response1.StatusCode);
            }

            protected override void When()
            {
                _response = MakeJsonPost(
                    TestStream,
                    new[] { new { EventId = _eventId, EventType = "event-type", Data = new { A = "1" } } });
            }

        }


        [TestFixture, Category("LongRunning")]
        class when_posting_an_event_three_times : HttpBehaviorSpecificationOfSuccessfulCreateEvent
        {
            private Guid _eventId;

            protected override void Given()
            {
                _eventId = Guid.NewGuid();
                var response1 = MakeJsonPost(
                    TestStream,
                    new[] { new { EventId = _eventId, EventType = "event-type", Data = new { A = "1" } } });
                Assert.AreEqual(HttpStatusCode.Created, response1.StatusCode);
                var response2 = MakeJsonPost(
                    TestStream,
                    new[] { new { EventId = _eventId, EventType = "event-type", Data = new { A = "1" } } });
                Assert.AreEqual(HttpStatusCode.Created, response2.StatusCode);
            }

            protected override void When()
            {
                _response = MakeJsonPost(
                    TestStream,
                    new[] { new { EventId = _eventId, EventType = "event-type", Data = new { A = "1" } } });
            }

        }

    }
}
