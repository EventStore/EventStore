using System;
using System.Net;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Http.Streams.basic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace EventStore.Core.Tests.Http.Streams
{
    [TestFixture]
    public class when_posting_metadata_as_json_to_non_existing_stream : HttpBehaviorSpecification
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
        }

        protected override void When()
        {
            var req = CreateRawJsonPostRequest(TestStream + "/metadata", "POST", new {A = "1"},
                new NetworkCredential("admin", "changeit"));
            req.Headers.Add("ES-EventId", Guid.NewGuid().ToString());
            _response = (HttpWebResponse) req.GetResponse();
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
            HelperExtensions.AssertJson(new { A = "1" }, json);
        }
    }

    [TestFixture]
    public class when_posting_metadata_as_json_to_existing_stream : HttpBehaviorSpecificationWithSingleEvent
    {
        protected override void Given()
        {
            _response = MakeArrayEventsPost(
                TestStream,
                new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}}});
        }

        protected override void When()
        {
            var req = CreateRawJsonPostRequest(TestStream + "/metadata", "POST", new { A = "1" },
                new NetworkCredential("admin", "changeit"));
            req.Headers.Add("ES-EventId", Guid.NewGuid().ToString());
            _response = (HttpWebResponse)req.GetResponse();
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
            HelperExtensions.AssertJson(new { A = "1" }, json);
        }
    }


}