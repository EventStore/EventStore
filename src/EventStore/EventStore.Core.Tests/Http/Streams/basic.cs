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
using EventStore.Core.Tests.Http.Users;
using EventStore.Transport.Http;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace EventStore.Core.Tests.Http.Streams
{
    namespace basic
    {
        [TestFixture, Category("LongRunning")]
        public class when_posting_an_event_as_array : HttpBehaviorSpecification
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
                HelperExtensions.AssertJson(new {A = "1"}, json);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_posting_an_event_as_array_to_stream_with_slash : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                var request = CreateRequest(TestStream + "/", "", "POST", "application/json", null);
                request.AllowAutoRedirect = false;
                request.GetRequestStream().WriteJson(new[] { new { EventId = Guid.NewGuid(), EventType = "event-type", Data = new { A = "1" } } });
                _response = (HttpWebResponse) request.GetResponse();
            }

            [Test]
            public void returns_permanent_redirect()
            {
                Assert.AreEqual(HttpStatusCode.MovedPermanently, _response.StatusCode);
            }

            [Test]
            public void returns_a_location_header()
            {
                Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
            }

            [Test]
            public void returns_a_location_header_that_is_to_stream_without_slash()
            {
                Assert.AreEqual(MakeUrl(TestStream), _response.Headers[HttpResponseHeader.Location]);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_deleting_to_stream_with_slash : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                var request = CreateRequest(TestStream + "/", "", "DELETE", "application/json", null);
                request.AllowAutoRedirect = false;
                _response = (HttpWebResponse)request.GetResponse();
            }

            [Test]
            public void returns_permanent_redirect()
            {
                Assert.AreEqual(HttpStatusCode.MovedPermanently, _response.StatusCode);
            }

            [Test]
            public void returns_a_location_header()
            {
                Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
            }

            [Test]
            public void returns_a_location_header_that_is_to_stream_without_slash()
            {
                Assert.AreEqual(MakeUrl(TestStream), _response.Headers[HttpResponseHeader.Location]);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_getting_from_stream_with_slash : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                var request = CreateRequest(TestStream + "/", "", "GET", "application/json", null);
                request.AllowAutoRedirect = false;
                _response = (HttpWebResponse)request.GetResponse();
            }

            [Test]
            public void returns_permanent_redirect()
            {
                Assert.AreEqual(HttpStatusCode.MovedPermanently, _response.StatusCode);
            }

            [Test]
            public void returns_a_location_header()
            {
                Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
            }

            [Test]
            public void returns_a_location_header_that_is_to_stream_without_slash()
            {
                Assert.AreEqual(MakeUrl(TestStream), _response.Headers[HttpResponseHeader.Location]);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_getting_from_all_stream_with_slash : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                var request = CreateRequest("/streams/$all/", "", "GET", "application/json", null);
                request.Credentials = new NetworkCredential("admin", "changeit");
                request.AllowAutoRedirect = false;
                _response = (HttpWebResponse)request.GetResponse();
            }

            [Test]
            public void returns_permanent_redirect()
            {
                Assert.AreEqual(HttpStatusCode.MovedPermanently, _response.StatusCode);
            }

            [Test]
            public void returns_a_location_header()
            {
                Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
            }

            [Test]
            public void returns_a_location_header_that_is_to_stream_without_slash()
            {
                Assert.AreEqual(MakeUrl("/streams/$all"), _response.Headers[HttpResponseHeader.Location]);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_getting_from_encoded_all_stream_with_slash : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                var request = CreateRequest("/streams/%24all/", "", "GET", "application/json", null);
                request.Credentials = new NetworkCredential("admin", "changeit");
                request.AllowAutoRedirect = false;
                _response = (HttpWebResponse)request.GetResponse();
            }

            [Test]
            public void returns_permanent_redirect()
            {
                Assert.AreEqual(HttpStatusCode.MovedPermanently, _response.StatusCode);
            }

            [Test]
            public void returns_a_location_header()
            {
                Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
            }

            [Test]
            public void returns_a_location_header_that_is_to_stream_without_slash()
            {
                Assert.AreEqual(MakeUrl("/streams/%24all").ToString(), _response.Headers[HttpResponseHeader.Location]);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_posting_an_event_as_array_to_metadata_stream_with_slash : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                var request = CreateRequest(TestStream + "/metadata/", "", "POST", "application/json", null);
                request.AllowAutoRedirect = false;
                request.GetRequestStream().WriteJson(new[] { new { EventId = Guid.NewGuid(), EventType = "event-type", Data = new { A = "1" } } });
                _response = (HttpWebResponse)request.GetResponse();
            }

            [Test]
            public void returns_permanent_redirect()
            {
                Assert.AreEqual(HttpStatusCode.MovedPermanently, _response.StatusCode);
            }

            [Test]
            public void returns_a_location_header()
            {
                Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
            }

            [Test]
            public void returns_a_location_header_that_is_to_stream_without_slash()
            {
                Assert.AreEqual(MakeUrl(TestStream + "/metadata"), _response.Headers[HttpResponseHeader.Location]);
            }
        }


        [TestFixture, Category("LongRunning")]
        public class when_getting_from_metadata_stream_with_slash : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                var request = CreateRequest(TestStream + "/metadata/", "", "GET", "application/json", null);
                request.Credentials = new NetworkCredential("admin", "changeit");
                request.AllowAutoRedirect = false;
                _response = (HttpWebResponse)request.GetResponse();
            }

            [Test]
            public void returns_permanent_redirect()
            {
                Assert.AreEqual(HttpStatusCode.MovedPermanently, _response.StatusCode);
            }

            [Test]
            public void returns_a_location_header()
            {
                Assert.IsNotEmpty(_response.Headers[HttpResponseHeader.Location]);
            }

            [Test]
            public void returns_a_location_header_that_is_to_stream_without_slash()
            {
                Assert.AreEqual(MakeUrl(TestStream + "/metadata/"), _response.Headers[HttpResponseHeader.Location]);
            }
        }



        [TestFixture, Category("LongRunning")]
        public class when_posting_an_event_without_EventId_as_array : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                _response = MakeJsonPost(
                    TestStream,
                    new[] { new { EventType = "event-type", Data = new { A = "1" } } });
            }

            [Test]
            public void returns_bad_request_status_code()
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_posting_an_event_without_EventType_as_array : HttpBehaviorSpecification
        {
            private HttpWebResponse _response;

            protected override void Given()
            {
            }

            protected override void When()
            {
                _response = MakeJsonPost(
                    TestStream,
                    new[] { new { EventId = Guid.NewGuid(), Data = new { A = "1" } } });
            }

            [Test]
            public void returns_bad_request_status_code()
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_posting_an_event_with_date_time : HttpBehaviorSpecification
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
                            new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1987-11-07T00:00:00.000+01:00"}},
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
            public void the_json_data_is_not_mangled()
            {
                var json = GetJson<JObject>(_response.Headers[HttpResponseHeader.Location]);
                HelperExtensions.AssertJson(new { A = "1987-11-07T00:00:00.000+01:00" }, json);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_posting_an_events_as_array : HttpBehaviorSpecification
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
                HelperExtensions.AssertJson(new {A = "1"}, json);
            }
        }

        public abstract class HttpBehaviorSpecificationWithSingleEvent : HttpBehaviorSpecification
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
        public class when_requesting_a_single_event_in_the_stream_as_atom_json: HttpBehaviorSpecificationWithSingleEvent
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
                HelperExtensions.AssertJson(new { Content = new { Data = new {A = "1"}}}, _json);
            }

        }



        [TestFixture, Category("LongRunning")]
        public class when_requesting_a_single_event_in_the_stream_as_event_json: HttpBehaviorSpecificationWithSingleEvent
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
                HelperExtensions.AssertJson(new { Data = new {A = "1"}}, _json);
            }

        }

        [TestFixture, Category("LongRunning")]
        public class when_requesting_a_single_event_in_the_stream_as_json: HttpBehaviorSpecificationWithSingleEvent
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
                HelperExtensions.AssertJson(new {A = "1"}, _json);
            }
        }


        [TestFixture, Category("LongRunning")]
        public class when_requesting_a_single_event_in_the_stream_as_atom_xml: HttpBehaviorSpecificationWithSingleEvent
        {
            //private JObject _json;

            protected override void When()
            {
                Get(TestStream + "/0", "", accept: ContentType.Atom);
            }

            [Test]
            public void request_succeeds()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

        }

        [TestFixture, Category("LongRunning")]
        public class when_requesting_a_single_event_in_the_stream_as_event_xml: HttpBehaviorSpecificationWithSingleEvent
        {
            //private JObject _json;

            protected override void When()
            {
                Get(TestStream + "/0", "", accept: ContentType.EventXml);
            }

            [Test]
            public void request_succeeds()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

        }

        [TestFixture, Category("LongRunning")]
        public class when_requesting_a_single_event_in_the_stream_as_xml: HttpBehaviorSpecificationWithSingleEvent
        {
            //private JObject _json;

            protected override void When()
            {
                Get(TestStream + "/0", "", accept: ContentType.Xml);
            }

            [Test]
            public void request_succeeds()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

        }

    }
}
