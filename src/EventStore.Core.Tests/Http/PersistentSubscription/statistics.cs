﻿using System;
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

namespace EventStore.Core.Tests.Http.PersistentSubscription
{
    [TestFixture, Category("LongRunning")]
    public class can_get_all_statistics_in_json : HttpBehaviorSpecification
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
        }

        protected override void When()
        {
            var request = CreateRequest("/subscriptions", null, "GET", "application/json", null);
            _response = GetRequestResponse(request);
        }

        [Test]
        public void the_response_code_is_ok()
        {
            Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class can_get_all_statistics_in_xml : HttpBehaviorSpecification
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
        }

        protected override void When()
        {
            var request = CreateRequest("/subscriptions", null, "GET", "text/xml", null);
            _response = GetRequestResponse(request);
        }

        [Test]
        public void the_response_code_is_ok()
        {
            Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class requesting_non_existing_single_stat_causes_404 : HttpBehaviorSpecification
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
        public void the_response_code_is_ok()
        {
            Assert.AreEqual(HttpStatusCode.NotFound, _response.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class requesting_non_existing_strea_stat_causes_404 : HttpBehaviorSpecification
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
        public void the_response_code_is_ok()
        {
            Assert.AreEqual(HttpStatusCode.NotFound, _response.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class subscription_stats_for_individual_have_right_data : SpecificationWithPersistentSubscriptionAndConnections
    {
        private JObject _json;


        protected override void When()
        {
            _json = GetJson<JObject>("/subscriptions/" + _streamName + "/" + _groupName, ContentType.Json);
        }

        [Test]
        public void the_response_code_is_ok()
        {
            Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
        }

        [Test]
        public void detail_rel_href_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}", _node.HttpEndPoint, _streamName, _groupName),
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
    public class subscription_stats_summary_has_right_data : SpecificationWithPersistentSubscriptionAndConnections
    {
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                    .DoNotResolveLinkTos()
                                                    .StartFromCurrent();

        private JArray _json;
        private EventStorePersistentSubscription _sub4;
        private EventStorePersistentSubscription _sub3;
        private EventStorePersistentSubscription _sub5;

        protected override void Given()
        {
            base.Given();
            _conn.CreatePersistentSubscriptionAsync(_streamName, "secondgroup", _settings,
                        new UserCredentials("admin", "changeit")).Wait();
            _sub3 = _conn.ConnectToPersistentSubscription("secondgroup", _streamName,
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine());
            _sub4 = _conn.ConnectToPersistentSubscription("secondgroup", _streamName,
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine(),
                        new UserCredentials("admin", "changeit"));
            _sub5 = _conn.ConnectToPersistentSubscription("secondgroup", _streamName,
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine(),
                        new UserCredentials("admin", "changeit"));

        }

        protected override void When()
        {
            //make mcs stop bitching
            Console.WriteLine(_sub3);
            Console.WriteLine(_sub4);
            Console.WriteLine(_sub5);
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
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}", _node.HttpEndPoint, _streamName, _groupName), 
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
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}", _node.HttpEndPoint, _streamName, "secondgroup"), 
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
            Assert.AreEqual(string.Format("http://{0}/streams/$persistentsubscription-{1}::{2}-parked",_node.HttpEndPoint, _streamName, _groupName), _json[0]["parkedMessageUri"].Value<string>());
        }

        [Test]
        public void the_second_parked_message_queue_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/streams/$persistentsubscription-{1}::{2}-parked", _node.HttpEndPoint, _streamName, "secondgroup"), _json[1]["parkedMessageUri"].Value<string>());
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
    public class subscription_stats_for_stream_have_summary_data : SpecificationWithPersistentSubscriptionAndConnections
    {
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                    .DoNotResolveLinkTos()
                                                    .StartFromCurrent();

        private JArray _json;
        private EventStorePersistentSubscription _sub4;
        private EventStorePersistentSubscription _sub3;
        private EventStorePersistentSubscription _sub5;

        protected override void Given()
        {
            base.Given();
            _conn.CreatePersistentSubscriptionAsync(_streamName, "secondgroup", _settings,
                        new UserCredentials("admin", "changeit")).Wait();
            _sub3 = _conn.ConnectToPersistentSubscription("secondgroup", _streamName,
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine());
            _sub4 = _conn.ConnectToPersistentSubscription("secondgroup", _streamName,
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine(),
                        new UserCredentials("admin", "changeit"));
            _sub5 = _conn.ConnectToPersistentSubscription("secondgroup", _streamName,
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine(),
                        new UserCredentials("admin", "changeit"));

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
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}", _node.HttpEndPoint, _streamName, _groupName), 
                _json[0]["links"][0]["href"].Value<string>());
        }

        [Test]
        public void the_second_event_stream_detail_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}", _node.HttpEndPoint, _streamName, "secondgroup"), 
                _json[1]["links"][0]["href"].Value<string>());
        }

        [Test]
        public void the_first_parked_message_queue_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/streams/$persistentsubscription-{1}::{2}-parked", _node.HttpEndPoint, _streamName, _groupName), _json[0]["parkedMessageUri"].Value<string>());
        }

        [Test]
        public void the_second_parked_message_queue_uri_is_correct()
        {
            Assert.AreEqual(string.Format("http://{0}/streams/$persistentsubscription-{1}::{2}-parked", _node.HttpEndPoint, _streamName, "secondgroup"), _json[1]["parkedMessageUri"].Value<string>());
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
        protected EventStorePersistentSubscription _sub1;
        protected EventStorePersistentSubscription _sub2;
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                    .DoNotResolveLinkTos()
                                                    .StartFromCurrent();

        protected override void Given()
        {
            _conn = EventStoreConnection.Create(_node.TcpEndPoint);
            _conn.ConnectAsync().Wait();
            _conn.CreatePersistentSubscriptionAsync(_streamName, _groupName, _settings,
                    new UserCredentials("admin", "changeit")).Wait();
            _sub1 = _conn.ConnectToPersistentSubscription(_groupName, _streamName,
                        (subscription, @event) => Console.WriteLine(), 
                        (subscription, reason, arg3) => Console.WriteLine());
            _sub2 = _conn.ConnectToPersistentSubscription(_groupName, _streamName,
                        (subscription, @event) => Console.WriteLine(),
                        (subscription, reason, arg3) => Console.WriteLine(),
                        new UserCredentials("admin", "changeit"));

        }

        protected override void When()
        {
            
        }

        [TestFixtureTearDown]
        public void Teardown()
        {
             _conn.DeletePersistentSubscriptionAsync(_streamName, _groupName, new UserCredentials("admin", "changeit")).Wait();
            _conn.Close();
            _conn.Dispose();
        }
    }
}