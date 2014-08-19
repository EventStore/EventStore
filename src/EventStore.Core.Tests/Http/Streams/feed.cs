﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.Chunks;
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
            protected override void When()
            {
                GetJson<JObject>(TestStream, ContentType.AtomJson);
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
        public class when_reading_a_stream_forward_with_deleted_linktos : HttpSpecificationWithLinkToToDeletedEvents
        {
            private JObject _feed;
            private List<JToken> _entries;
            protected override void When()
            {
                _feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/forward/10", accept: ContentType.Json);
                _entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
            }

            [Test]
            public void the_feed_has_one_event()
            {
                Assert.AreEqual(1, _entries.Count());
            }

            [Test]
            public void the_edit_link_to_is_to_correct_uri()
            {
                var foo = _entries[0]["links"][0];
                Assert.AreEqual("edit", foo["relation"].ToString());
                Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
            }

            [Test]
            public void the_alt_link_to_is_to_correct_uri()
            {
                var foo = _entries[0]["links"][1];
                Assert.AreEqual("alternate", foo["relation"].ToString());
                Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_reading_a_stream_forward_with_maxcount_deleted_linktos : SpecificationWithLinkToToMaxCountDeletedEvents
        {
            private JObject _feed;
            private List<JToken> _entries;
            protected override void When()
            {
                _feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/forward/10", accept: ContentType.Json);
                _entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
            }

            [Test]
            public void the_feed_has_no_events()
            {
                Assert.AreEqual(1, _entries.Count());
            }
        }

        [TestFixture, Category("LongRunning")][Explicit("Failing test for Greg demonstrating NullReferenceException in Convert.cs")]
        public class when_reading_a_stream_forward_with_maxcount_deleted_linktos_with_rich_entry : SpecificationWithLinkToToMaxCountDeletedEvents
        {
            private JObject _feed;
            private List<JToken> _entries;
            protected override void When()
            {
                _feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/forward/10?embed=rich", accept: ContentType.Json); 
                _entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
            }

            [Test]
            public void the_feed_has_some_events()
            {
                Assert.AreEqual(1, _entries.Count());
            }
        }

        [TestFixture, Category("LongRunning")]
        public class when_reading_a_stream_forward_with_deleted_linktos_with_content_enabled_as_xml :
            HttpSpecificationWithLinkToToDeletedEvents
        {
            private XDocument _feed;
            private XElement[] _entries;

            protected override void When()
            {
                _feed = GetXml("/streams/" + LinkedStreamName + "/0/forward/10?embed=content");
                _entries = _feed.GetEntries();
            }

            [Test]
            public void the_feed_has_one_event()
            {
                Assert.AreEqual(1, _entries.Length);
            }

            [Test]
            public void the_edit_link_is_to_correct_uri()
            {
                var link = _entries[0].GetLink("edit");
                Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), link);
            }

	    [Test]
            public void the_alternate_link_is_to_correct_uri()
            {
                var link = _entries[0].GetLink("alternate");
                Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), link);
            }
        }


        [TestFixture, Category("LongRunning")]
        public class when_reading_a_stream_forward_with_deleted_linktos_with_content_enabled : HttpSpecificationWithLinkToToDeletedEvents
        {
            private JObject _feed;
            private List<JToken> _entries;
            protected override void When()
            {
                _feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/forward/10?embed=content", accept: ContentType.Json);
                _entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
            }

            [Test]
            public void the_feed_has_one_event()
            {
                Assert.AreEqual(1, _entries.Count());
            }

            [Test]
            public void the_edit_link_to_is_to_correct_uri()
            {
                var foo = _entries[0]["links"][0];
                Assert.AreEqual("edit", foo["relation"].ToString());
                Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
            }

            [Test]
            public void the_alt_link_to_is_to_correct_uri()
            {
                var foo = _entries[0]["links"][1];
                Assert.AreEqual("alternate", foo["relation"].ToString());
                Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
            }
        }


        [TestFixture, Category("LongRunning")]
        public class when_reading_a_stream_backward_with_deleted_linktos : HttpSpecificationWithLinkToToDeletedEvents
        {
            private JObject _feed;
            private List<JToken> _entries;
            protected override void When()
            {
                _feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/backward/1", accept: ContentType.Json);
                _entries = _feed != null? _feed["entries"].ToList() : new List<JToken>();
            }

            [Test]
            public void the_feed_has_one_event()
            {
                Assert.AreEqual(1, _entries.Count());
            }

            [Test]
            public void the_edit_link_to_is_to_correct_uri()
            {
                var foo = _entries[0]["links"][0];
                Assert.AreEqual("edit", foo["relation"].ToString());
                Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
            }

            [Test]
            public void the_alt_link_to_is_to_correct_uri()
            {
                var foo = _entries[0]["links"][1];
                Assert.AreEqual("alternate", foo["relation"].ToString());
                Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
            }
        }


        [TestFixture, Category("LongRunning")]
        public class when_reading_a_stream_backward_with_deleted_linktos_and_embed_of_content : HttpSpecificationWithLinkToToDeletedEvents
        {
            private JObject _feed;
            private List<JToken> _entries;
            protected override void When()
            {
                _feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/backward/1", accept: ContentType.Json);
                _entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
            }

            [Test]
            public void the_feed_has_one_event()
            {
                Assert.AreEqual(1, _entries.Count());
            }

            [Test]
            public void the_edit_link_to_is_to_correct_uri()
            {
                var foo = _entries[0]["links"][0];
                Assert.AreEqual("edit", foo["relation"].ToString());
                Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
            }

            [Test]
            public void the_alt_link_to_is_to_correct_uri()
            {
                var foo = _entries[0]["links"][1];
                Assert.AreEqual("alternate", foo["relation"].ToString());
                Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
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
