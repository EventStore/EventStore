using System;
using System.Net;
using System.Text.RegularExpressions;
using EventStore.Core.Tests.Http.Users.users;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;
using EventStore.Transport.Http;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common;

// ReSharper disable InconsistentNaming

namespace EventStore.Core.Tests.Http.PersistentSubscription
{
    [TestFixture, Category("LongRunning")]
    class with_subscription_having_events : with_admin_user
    {
        protected List<object> Events;
        protected string SubscriptionPath;
        protected string GroupName;
        protected int? NumberOfEventsToCreate;

        protected override void Given()
        {
            Events = new List<object>
            {
                new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}},
                new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {B = "2"}},
                new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {C = "3"}},
                new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {D = "4"}}
            };

            var response = MakeArrayEventsPost(
                         TestStream,
                         Events.Take(NumberOfEventsToCreate ?? Events.Count),
                         _admin);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            GroupName = Guid.NewGuid().ToString();
            SubscriptionPath = string.Format("/subscriptions/{0}/{1}", TestStream.Substring(9), GroupName);
            response = MakeJsonPut(SubscriptionPath,
                new
                {
                    ResolveLinkTos = true,
                    MessageTimeoutMilliseconds = 10000,
                },
                _admin);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        protected override void When()
        {

        }

        protected void SecureStream()
        {
            var metadata =
                (StreamMetadata)
                StreamMetadata.Build()
                              .SetMetadataReadRole("admin")
                              .SetMetadataWriteRole("admin")
                              .SetReadRole("admin")
                              .SetWriteRole("admin");
            var jsonMetadata = metadata.AsJsonString();
            var response = MakeArrayEventsPost(
                TestMetadataStream,
                new[]
                    {
                            new
                                {
                                    EventId = Guid.NewGuid(),
                                    EventType = SystemEventTypes.StreamMetadata,
                                    Data = new JRaw(jsonMetadata)
                                }
                    });
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_getting_messages_without_permission : with_subscription_having_events
    {
        protected override void Given()
        {
            base.Given();
            SecureStream();
        }
        protected override void When()
        {
            GetJson<JObject>(
               SubscriptionPath,
               ContentType.AtomJson);
        }

        [Test]
        public void returns_unauthorised()
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, _lastResponse.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_getting_messages_from_an_empty_subscription : with_admin_user
    {
        private JObject _response;
        protected List<object> Events;
        protected string SubscriptionPath;
        protected string GroupName;

        protected override void Given()
        {
            Events = new List<object>
            {
                new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}},
                new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {B = "2"}},
                new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {C = "3"}},
                new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {D = "4"}}
            };

            var response = MakeArrayEventsPost(
                         TestStream,
                         Events,
                         _admin);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            GroupName = Guid.NewGuid().ToString();
            SubscriptionPath = string.Format("/subscriptions/{0}/{1}", TestStream.Substring(9), GroupName);
            response = MakeJsonPut(SubscriptionPath,
                new
                {
                    ResolveLinkTos = true,
                    MessageTimeoutMilliseconds = 10000
                },
                _admin);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            //pull all events out.
            _response = GetJson<JObject>(
                       SubscriptionPath + "/" + Events.Count,
                       ContentType.AtomJson, //todo CLC sort out allowed content types
                       _admin);

            var count = ((JObject)_response)["entries"].Count();
            Assert.AreEqual(Events.Count, count, "Expected {0} events, received {1}", Events.Count, count);
        }

        protected override void When()
        {
            _response = GetJson<JObject>(
                      SubscriptionPath,
                      ContentType.AtomJson,
                      _admin);
        }

        [Test]
        public void return_0_messages()
        {
            var count = ((JObject)_response)["entries"].Count();
            Assert.AreEqual(0, count, "Expected {0} events, received {1}", 0, count);
        }
    }

    class when_getting_messages_from_a_subscription_with_n_messages : with_subscription_having_events
    {
        private JObject _response;

        protected override void When()
        {

            _response = GetJson<JObject>(
                                SubscriptionPath + "/" + Events.Count,
                                ContentType.AtomJson, //todo CLC sort out allowed content types
                                _admin);
        }

        [Test]
        public void returns_n_messages()
        {
            var count = ((JObject)_response)["entries"].Count();
            Assert.AreEqual(Events.Count, count, "Expected {0} events, received {1}", Events.Count, count);
        }
    }

    class when_getting_messages_from_a_subscription_with_more_than_n_messages : with_subscription_having_events
    {
        private JObject _response;

        protected override void When()
        {

            _response = GetJson<JObject>(
                                SubscriptionPath + "/" + (Events.Count - 1),
                                ContentType.AtomJson, //todo CLC sort out allowed content types
                                _admin);
        }

        [Test]
        public void returns_n_messages()
        {
            var count = ((JArray)_response["entries"]).Count;
            Assert.AreEqual(Events.Count - 1, count, "Expected {0} events, received {1}", Events.Count - 1, count);
        }
    }

    class when_getting_messages_from_a_subscription_with_less_than_n_messags : with_subscription_having_events
    {
        private JObject _response;

        protected override void When()
        {
            _response = GetJson<JObject>(
                                SubscriptionPath + "/" + (Events.Count + 1),
                                ContentType.AtomJson, //todo CLC sort out allowed content types
                                _admin);
        }

        [Test]
        public void returns_all_messages()
        {
            var count = ((JArray)_response["entries"]).Count;
            Assert.AreEqual(Events.Count, count, "Expected {0} events, received {1}", Events.Count, count);
        }
    }
    class when_getting_messages_from_a_subscription_with_unspecified_count : with_subscription_having_events
    {
        private JObject _response;

        protected override void When()
        {

            _response = GetJson<JObject>(
                                SubscriptionPath,
                                ContentType.AtomJson,
                                _admin);
        }

        [Test]
        public void returns_1_message()
        {
            var count = ((JArray)_response["entries"]).Count;
            Assert.AreEqual(1, count, "Expected {0} events, received {1}", 1, count);
        }
    }

    class when_getting_messages_from_a_subscription_with_a_negative_count : with_subscription_having_events
    {
        protected override void When()
        {
            Get(SubscriptionPath + "/-1",
                "",
                ContentType.AtomJson,
                _admin);
        }

        [Test]
        public void returns_bad_request()
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);
        }
    }

    class when_getting_messages_from_a_subscription_with_a_count_of_0 : with_subscription_having_events
    {
        protected override void When()
        {
            Get(SubscriptionPath + "/0",
              "",
              ContentType.AtomJson,
              _admin);
        }

        [Test]
        public void returns_bad_request()
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);
        }
    }

    class when_getting_messages_from_a_subscription_with_count_more_than_100 : with_subscription_having_events
    {
        protected override void When()
        {
            Get(SubscriptionPath + "/101",
                "",
                ContentType.AtomJson,
                _admin);
        }

        [Test]
        public void returns_bad_request()
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);
        }
    }

    class when_getting_messages_from_a_subscription_with_count_not_an_integer : with_subscription_having_events
    {
        protected override void When()
        {
            Get(SubscriptionPath + "/10.1",
              "",
              ContentType.AtomJson,
              _admin);
        }

        [Test]
        public void returns_bad_request()
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);
        }
    }

    class when_getting_messages_from_a_subscription_with_count_not_a_number : with_subscription_having_events
    {
        protected override void When()
        {
            Get(SubscriptionPath + "/one",
            "",
            ContentType.AtomJson,
            _admin);
        }

        [Test]
        public void returns_bad_request()
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);
        }
    }
}