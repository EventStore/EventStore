using System;
using System.Net;
using EventStore.Core.Tests.Helpers;
using EventStore.Transport.Http;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;

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
                var response = MakeArrayEventsPost(
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
            public void returns_200_response()
            {
                Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            }

            [Test]
            public void there_is_no_prev_link()
            {
                var rel = GetLink(_feed, "prev");
                Assert.IsNull(rel);
            }

            [Test]
            public void there_is_a_next_link()
            {
                var rel = GetLink(_feed, "next");
                Assert.IsNotEmpty(rel);                
            }

            [Test]
            public void there_is_a_self_link()
            {
                var rel = GetLink(_feed, "self");
                Assert.IsNotEmpty(rel);
            }

            [Test]
            public void there_is_a_first_link()
            {
                var rel = GetLink(_feed, "first");
                Assert.IsNotEmpty(rel);
            }

            [Test]
            public void there_is_a_last_link()
            {
                var rel = GetLink(_feed, "last");
                Assert.IsNotEmpty(rel);
            }

            [Test]
            public void the_feed_is_empty()
            {
                Assert.AreEqual(0, _feed["entries"].Count());
            }

            [Test]
            public void the_response_is_not_cachable()
            {
                Assert.AreEqual("max-age=0, no-cache, must-revalidate", _lastResponse.Headers["Cache-Control"]);
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
                HelperExtensions.AssertJson(new {entries = new[] {new {Id = _lastEventLocation}}}, _feed);
            }
        }

    }
}
