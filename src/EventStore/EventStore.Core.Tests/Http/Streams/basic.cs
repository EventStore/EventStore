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
using EventStore.Transport.Http;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace EventStore.Core.Tests.Http.Streams
{
    namespace basic
    {
        [TestFixture, Category("LongRunning")]
        class when_posting_an_event_as_array : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                _response = MakeJsonPost(
                    TestStream,
                    new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}}});
            }

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
            public void returns_a_location_header_that_can_be_read_as_json()
            {
                var json = GetJson<JObject>(_response.Headers[HttpResponseHeader.Location]);
                AssertJson(new {A = "1"}, json);
            }
        }

        [TestFixture, Category("LongRunning")]
        class when_posting_an_events_as_array : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                _response = MakeJsonPost(
                    TestStream,
                    new[]
                        {
                            new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}},
                            new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "2"}},
                        });
            }

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
            public void returns_a_location_header_for_the_first_posted_event()
            {
                var json = GetJson<JObject>(_response.Headers[HttpResponseHeader.Location]);
                AssertJson(new {A = "1"}, json);
            }
        }

        abstract class HttpBehaviorSpecificationWithSingleEvent : HttpBehaviorSpecification
        {
            protected HttpWebResponse _response;

            protected override void Given()
            {
                _response = MakeJsonPost(
                    TestStream,
                    new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}}});
                Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
            }

        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_single_event_in_the_stream_as_atom_json: HttpBehaviorSpecificationWithSingleEvent
        {
            private JObject _json;

            protected override void When()
            {
                _json = GetJson<JObject>(TestStream + "/0", accept: ContentType.AtomJson);
            }

            [Test]
            public void request_succeeds()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

            [Test]
            public void returns_correct_body()
            {
                AssertJson(new { Content = new { Data = new {A = "1"}}}, _json);
            }

        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_single_event_in_the_stream_as_event_json: HttpBehaviorSpecificationWithSingleEvent
        {
            private JObject _json;

            protected override void When()
            {
                _json = GetJson<JObject>(TestStream + "/0", accept: ContentType.EventJson);
            }

            [Test]
            public void request_succeeds()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

            [Test]
            public void returns_correct_body()
            {
                AssertJson(new { Data = new {A = "1"}}, _json);
            }

        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_single_event_in_the_stream_as_json: HttpBehaviorSpecificationWithSingleEvent
        {
            private JObject _json;

            protected override void When()
            {
                _json = GetJson<JObject>(TestStream + "/0", accept: ContentType.Json);
            }

            [Test]
            public void request_succeeds()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

            [Test]
            public void returns_correct_body()
            {
                AssertJson(new {A = "1"}, _json);
            }
        }


        [TestFixture, Category("LongRunning")]
        class when_requesting_a_single_event_in_the_stream_as_atom_xml: HttpBehaviorSpecificationWithSingleEvent
        {
            private JObject _json;

            protected override void When()
            {
                Get(TestStream + "/0", accept: ContentType.Atom);
            }

            [Test]
            public void request_succeeds()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_single_event_in_the_stream_as_event_xml: HttpBehaviorSpecificationWithSingleEvent
        {
            private JObject _json;

            protected override void When()
            {
                Get(TestStream + "/0", accept: ContentType.EventXml);
            }

            [Test]
            public void request_succeeds()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

        }

        [TestFixture, Category("LongRunning")]
        class when_requesting_a_single_event_in_the_stream_as_xml: HttpBehaviorSpecificationWithSingleEvent
        {
            private JObject _json;

            protected override void When()
            {
                Get(TestStream + "/0", accept: ContentType.Xml);
            }

            [Test]
            public void request_succeeds()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

        }

    }
}
