using System;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Tests.Http.BasicAuthentication.basic_authentication;
using EventStore.Transport.Http;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using HttpStatusCode = System.Net.HttpStatusCode;
using EventStore.Core.Tests.Helpers;
using System.Xml.Linq;

namespace EventStore.Core.Tests.Http.PersistentSubscription
{
    [TestFixture, Category("LongRunning")]
    class when_getting_all_statistics_in_json : with_subscription_having_events
    {
        private JArray _json;

        protected override void When()
        {
            _json = GetJson<JArray>("/subscriptions", accept: ContentType.Json);
        }

        [Test]
        public void returns_ok()
        {
            Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
        }

        [Test]
        public void body_contains_valid_json()
        {
            Assert.AreEqual(TestStreamName, _json[0]["eventStreamId"].Value<string>());
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_getting_all_statistics_in_xml : with_subscription_having_events
    {
        private XDocument _xml;

        protected override void When()
        {
            _xml = GetXml(MakeUrl("/subscriptions"));
        }

        [Test]
        public void returns_ok()
        {
            Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
        }

        [Test]
        public void body_contains_valid_xml()
        {
            Assert.AreEqual(TestStreamName, _xml.Descendants("EventStreamId").First().Value);
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_getting_non_existent_single_statistics : HttpBehaviorSpecification
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
        }

        protected override void When()
        {
            var request = CreateRequest("/subscriptions/fu/fubar", null, "GET", "text/xml", null);
            _response = GetRequestResponse(request);
        }

        [Test]
        public void returns_not_found()
        {
            Assert.AreEqual(HttpStatusCode.NotFound, _response.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_getting_non_existent_stream_statistics : HttpBehaviorSpecification
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
        }

        protected override void When()
        {
            var request = CreateRequest("/subscriptions/fubar", null, "GET", "text/xml", null);
            _response = GetRequestResponse(request);
        }

        [Test]
        public void returns_not_found()
        {
            Assert.AreEqual(HttpStatusCode.NotFound, _response.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_getting_subscription_statistics_for_individual : SpecificationWithPersistentSubscriptionAndConnections
    {
        private JObject _json;


        protected override void When()
        {
            _json = GetJson<JObject>("/subscriptions/" + _streamName + "/" + _groupName + "/info", ContentType.Json);
        }

        [Test]
        public void returns_ok()
        {
            Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
        }

        [Test]
        public void detail_rel_href_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}/info", _node.ExtHttpEndPoint, _streamName, _groupName),
                _json["links"][0]["href"].Value<string>());
        }

        [Test]
        public void has_two_rel_links()
        {
            Assert.AreEqual(2,
                _json["links"].Count());
        }

        [Test]
        public void the_view_detail_rel_is_correct()
        {
            Assert.AreEqual("detail",
                _json["links"][0]["rel"].Value<string>());
        }

        [Test]
        public void the_event_stream_is_correct()
        {
            Assert.AreEqual(_streamName, _json["eventStreamId"].Value<string>());
        }

        [Test]
        public void the_groupname_is_correct()
        {
            Assert.AreEqual(_groupName, _json["groupName"].Value<string>());
        }

        [Test]
        public void the_status_is_live()
        {
            Assert.AreEqual("Live", _json["status"].Value<string>());
        }

        [Test]
        public void there_are_two_connections()
        {
            Assert.AreEqual(2, _json["connections"].Count());
        }

        [Test]
        public void the_first_connection_has_endpoint()
        {
            Assert.IsNotNull(_json["connections"][0]["from"]);
        }

        [Test]
        public void the_second_connection_has_endpoint()
        {
            Assert.IsNotNull(_json["connections"][1]["from"]);
        }

        [Test]
        public void the_first_connection_has_user()
        {
            Assert.AreEqual("anonymous", _json["connections"][0]["username"].Value<string>());
        }

        [Test]
        public void the_second_connection_has_user()
        {
            Assert.AreEqual("admin", _json["connections"][1]["username"].Value<string>());
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_getting_subscription_stats_summary : SpecificationWithPersistentSubscriptionAndConnections
    {
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                    .DoNotResolveLinkTos()
                                                    .StartFromCurrent();

        private JArray _json;

        protected override void Given()
        {
            base.Given();
            _conn.CreatePersistentSubscriptionAsync(_streamName, "secondgroup", _settings,
                        DefaultData.AdminCredentials).Wait();
            _conn.ConnectToPersistentSubscription(_streamName, "secondgroup",
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine());
            _conn.ConnectToPersistentSubscription(_streamName, "secondgroup",
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine(),
                        DefaultData.AdminCredentials);
            _conn.ConnectToPersistentSubscription(_streamName, "secondgroup",
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine(),
                        DefaultData.AdminCredentials);

        }

        protected override void When()
        {
            _json = GetJson<JArray>("/subscriptions", ContentType.Json);
        }

        [Test]
        public void the_response_code_is_ok()
        {
            Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
        }

        [Test]
        public void the_first_event_stream_is_correct()
        {
            Assert.AreEqual(_streamName, _json[0]["eventStreamId"].Value<string>());
        }

        [Test]
        public void the_first_groupname_is_correct()
        {
            Assert.AreEqual(_groupName, _json[0]["groupName"].Value<string>());
        }

        [Test]
        public void the_first_event_stream_detail_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}/info", _node.ExtHttpEndPoint, _streamName, _groupName),
                _json[0]["links"][0]["href"].Value<string>());
        }

        [Test]
        public void the_first_event_stream_detail_has_one_link()
        {
            Assert.AreEqual(1,
                _json[0]["links"].Count());
        }

        [Test]
        public void the_first_event_stream_detail_rel_is_correct()
        {
            Assert.AreEqual("detail",
                _json[0]["links"][0]["rel"].Value<string>());
        }

        [Test]
        public void the_second_event_stream_detail_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}/info", _node.ExtHttpEndPoint, _streamName, "secondgroup"),
                _json[1]["links"][0]["href"].Value<string>());
        }

        [Test]
        public void the_second_event_stream_detail_has_one_link()
        {
            Assert.AreEqual(1,
                _json[1]["links"].Count());
        }

        [Test]
        public void the_second_event_stream_detail_rel_is_correct()
        {
            Assert.AreEqual("detail",
                _json[1]["links"][0]["rel"].Value<string>());
        }

        [Test]
        public void the_first_parked_message_queue_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/streams/$persistentsubscription-{1}::{2}-parked", _node.ExtHttpEndPoint, _streamName, _groupName), _json[0]["parkedMessageUri"].Value<string>());
        }

        [Test]
        public void the_second_parked_message_queue_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/streams/$persistentsubscription-{1}::{2}-parked", _node.ExtHttpEndPoint, _streamName, "secondgroup"), _json[1]["parkedMessageUri"].Value<string>());
        }

        [Test]
        public void the_status_is_live()
        {
            Assert.AreEqual("Live", _json[0]["status"].Value<string>());
        }

        [Test]
        public void there_are_two_connections()
        {
            Assert.AreEqual(2, _json[0]["connectionCount"].Value<int>());
        }

        [Test]
        public void the_second_subscription_event_stream_is_correct()
        {
            Assert.AreEqual(_streamName, _json[1]["eventStreamId"].Value<string>());
        }

        [Test]
        public void the_second_subscription_groupname_is_correct()
        {
            Assert.AreEqual("secondgroup", _json[1]["groupName"].Value<string>());
        }

        [Test]
        public void second_subscription_there_are_three_connections()
        {
            Assert.AreEqual(3, _json[1]["connectionCount"].Value<int>());
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_getting_subscription_statistics_for_stream : SpecificationWithPersistentSubscriptionAndConnections
    {
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                    .DoNotResolveLinkTos()
                                                    .StartFromCurrent();

        private JArray _json;
        private EventStorePersistentSubscriptionBase _sub4;
        private EventStorePersistentSubscriptionBase _sub3;
        private EventStorePersistentSubscriptionBase _sub5;

        protected override void Given()
        {
            base.Given();
            _conn.CreatePersistentSubscriptionAsync(_streamName, "secondgroup", _settings,
                        DefaultData.AdminCredentials).Wait();
            _sub3 = _conn.ConnectToPersistentSubscription(_streamName, "secondgroup",
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine());
            _sub4 = _conn.ConnectToPersistentSubscription(_streamName, "secondgroup",
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine(),
                        DefaultData.AdminCredentials);
            _sub5 = _conn.ConnectToPersistentSubscription(_streamName, "secondgroup",
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine(),
                        DefaultData.AdminCredentials);

        }

        protected override void When()
        {
            //make mcs stop bitching
            Console.WriteLine(_sub3);
            Console.WriteLine(_sub4);
            Console.WriteLine(_sub5);
            _json = GetJson<JArray>("/subscriptions/" + _streamName, ContentType.Json);
        }

        [Test]
        public void the_response_code_is_ok()
        {
            Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
        }

        [Test]
        public void the_first_event_stream_is_correct()
        {
            Assert.AreEqual(_streamName, _json[0]["eventStreamId"].Value<string>());
        }

        [Test]
        public void the_first_groupname_is_correct()
        {
            Assert.AreEqual(_groupName, _json[0]["groupName"].Value<string>());
        }

        [Test]
        public void the_first_event_stream_detail_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}/info", _node.ExtHttpEndPoint, _streamName, _groupName),
                _json[0]["links"][0]["href"].Value<string>());
        }

        [Test]
        public void the_second_event_stream_detail_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}/info", _node.ExtHttpEndPoint, _streamName, "secondgroup"),
                _json[1]["links"][0]["href"].Value<string>());
        }

        [Test]
        public void the_first_parked_message_queue_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/streams/$persistentsubscription-{1}::{2}-parked", _node.ExtHttpEndPoint, _streamName, _groupName), _json[0]["parkedMessageUri"].Value<string>());
        }

        [Test]
        public void the_second_parked_message_queue_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/streams/$persistentsubscription-{1}::{2}-parked", _node.ExtHttpEndPoint, _streamName, "secondgroup"), _json[1]["parkedMessageUri"].Value<string>());
        }

        [Test]
        public void the_status_is_live()
        {
            Assert.AreEqual("Live", _json[0]["status"].Value<string>());
        }

        [Test]
        public void there_are_two_connections()
        {
            Assert.AreEqual(2, _json[0]["connectionCount"].Value<int>());
        }

        [Test]
        public void the_second_subscription_event_stream_is_correct()
        {
            Assert.AreEqual(_streamName, _json[1]["eventStreamId"].Value<string>());
        }

        [Test]
        public void the_second_subscription_groupname_is_correct()
        {
            Assert.AreEqual("secondgroup", _json[1]["groupName"].Value<string>());
        }

        [Test]
        public void second_subscription_there_are_three_connections()
        {
            Assert.AreEqual(3, _json[1]["connectionCount"].Value<int>());
        }
    }

    public abstract class SpecificationWithPersistentSubscriptionAndConnections : with_admin_user
    {
        protected string _streamName = Guid.NewGuid().ToString();
        protected string _groupName = Guid.NewGuid().ToString();
        protected IEventStoreConnection _conn;
        protected EventStorePersistentSubscriptionBase _sub1;
        protected EventStorePersistentSubscriptionBase _sub2;
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                    .DoNotResolveLinkTos()
                                                    .StartFromCurrent();

        protected override void Given()
        {
            _conn = EventStoreConnection.Create(_node.TcpEndPoint);
            _conn.ConnectAsync().Wait();
            _conn.CreatePersistentSubscriptionAsync(_streamName, _groupName, _settings,
                    DefaultData.AdminCredentials).Wait();
            _sub1 = _conn.ConnectToPersistentSubscription(_streamName, _groupName,
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine());
            _sub2 = _conn.ConnectToPersistentSubscription(_streamName, _groupName,
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine(),
                        DefaultData.AdminCredentials);

        }

        protected override void When()
        {

        }

        [TestFixtureTearDown]
        public void Teardown()
        {
            _conn.DeletePersistentSubscriptionAsync(_streamName, _groupName, DefaultData.AdminCredentials).Wait();
            _conn.Close();
            _conn.Dispose();
        }
    }
}