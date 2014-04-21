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
                var response1 = MakeArrayEventsPost(
                    TestStream,
                    new[] { new { EventId = _eventId, EventType = "event-type", Data = new { A = "1" } } });
                Assert.AreEqual(HttpStatusCode.Created, response1.StatusCode);
            }

            protected override void When()
            {
                _response = MakeArrayEventsPost(
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
                var response1 = MakeArrayEventsPost(
                    TestStream,
                    new[] { new { EventId = _eventId, EventType = "event-type", Data = new { A = "1" } } });
                Assert.AreEqual(HttpStatusCode.Created, response1.StatusCode);
                var response2 = MakeArrayEventsPost(
                    TestStream,
                    new[] { new { EventId = _eventId, EventType = "event-type", Data = new { A = "1" } } });
                Assert.AreEqual(HttpStatusCode.Created, response2.StatusCode);
            }

            protected override void When()
            {
                _response = MakeArrayEventsPost(
                    TestStream,
                    new[] { new { EventId = _eventId, EventType = "event-type", Data = new { A = "1" } } });
            }

        }

    }
}
