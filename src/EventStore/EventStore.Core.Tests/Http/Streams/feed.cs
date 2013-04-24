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
using System.Linq;

namespace EventStore.Core.Tests.Http.Streams
{
    namespace feed
    {
        public abstract class SpecificationWithLongFeed : HttpBehaviorSpecification
        {
            private int _numberOfEvents;

            protected override void Given()
            {
                _numberOfEvents = 25;
                for (var i = 0; i < _numberOfEvents; i++)
                {
                    PostEvent(i);
                }
            }

            protected string PostEvent(int i)
            {
                var response = MakeJsonPost(
                    TestStream, new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {Number = i}}});
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                return response.Headers[HttpResponseHeader.Location];
            }

            protected string GetLink(JObject feed, string relation)
            {
                var rel = (from JObject link in feed["links"]
                           from JProperty attr in link
                           where attr.Name == "relation" && (string) attr.Value == relation
                           select link).SingleOrDefault();
                return (rel == null) ? (string)null : (string)rel["uri"];
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_posting_multiple_events : SpecificationWithLongFeed
        {
            private JObject _feed;

            protected override void When()
            {
                _feed = GetJson<JObject>(TestStream, ContentType.AtomJson);
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_retrieving_feed_head : SpecificationWithLongFeed
        {
            private JObject _feed;

            protected override void When()
            {
                _feed = GetJson<JObject>(TestStream, ContentType.AtomJson);
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

            [Test]
            public void contains_a_link_rel_previous()
            {
                var rel = GetLink(_feed, "previous");
                Assert.IsNotEmpty(rel);
            }

            [Test]
            public void contains_a_link_rel_next()
            {
                var rel = GetLink(_feed, "next");
                Assert.IsNotEmpty(rel);
            }

            [Test]
            public void contains_a_link_rel_self()
            {
                var rel = GetLink(_feed, "self");
                Assert.IsNotEmpty(rel);
            }

            [Test]
            public void contains_a_link_rel_first()
            {
                var rel = GetLink(_feed, "first");
                Assert.IsNotEmpty(rel);
            }

            [Test]
            public void contains_a_link_rel_last()
            {
                var rel = GetLink(_feed, "last");
                Assert.IsNotEmpty(rel);
            }
        }


        [TestFixture, Category("LongRunning")]
        public class when_retrieving_the_previous_link_of_the_feed_head: SpecificationWithLongFeed
        {
            private JObject _feed;
            private JObject _head;
            private string _previous;

            protected override void Given()
            {
                base.Given();
                _head = GetJson<JObject>(TestStream, ContentType.AtomJson);
                _previous = GetLink(_head, "previous");
            }

            protected override void When()
            {
                _feed = GetJson<JObject>(_previous, ContentType.AtomJson);
            }

            [Test]
            public void returns_not_found_status_code()
            {
                Assert.AreEqual(HttpStatusCode.NotFound, _lastResponse.StatusCode);
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_polling_the_head_forward_and_a_new_event_appears: SpecificationWithLongFeed
        {
            private JObject _feed;
            private JObject _head;
            private string _previous;
            private string _lastEventLocation;

            protected override void Given()
            {
                base.Given();
                _head = GetJson<JObject>(TestStream, ContentType.AtomJson);
                _previous = GetLink(_head, "previous");
                _lastEventLocation = PostEvent(-1);
            }

            protected override void When()
            {
                _feed = GetJson<JObject>(_previous, ContentType.AtomJson);
            }

            [Test]
            public void returns_ok_status_code()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

            [Test]
            public void returns_a_feed_with_a_single_entry_referring_to_the_last_event()
            {
                AssertJson(new {entries = new[] {new {Id = _lastEventLocation}}}, _feed);
            }
        }

    }
}
