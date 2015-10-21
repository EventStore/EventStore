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
using EventStore.Core.Data;

namespace EventStore.Core.Tests.Http.PersistentSubscription
{
    class when_parking_a_message : with_subscription_having_events
    {
        private string _nackLink;
        private Guid _eventIdToPark;
        private Guid _parkedEventId;
        private List<JToken> _entries;
        private AutoResetEvent _eventParked = new AutoResetEvent(false);
        protected override void Given()
        {
            NumberOfEventsToCreate = 1;
            base.Given();
            var json = GetJson<JObject>(
               SubscriptionPath + "/1?embed=rich",
               ContentType.AtomJson,
               _admin);
            Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
            _entries = json != null ? json["entries"].ToList() : new List<JToken>();
            _nackLink = _entries[0]["links"][3]["uri"].ToString() + "?action=park";
            _eventIdToPark = Guid.Parse(_entries[0]["eventId"].ToString());
        }

        protected override void When()
        {
            var parkedStreamId = String.Format("$persistentsubscription-{0}::{1}-parked", TestStreamName, GroupName);

            _connection.SubscribeToStreamAsync(parkedStreamId, true, (x, y) =>
            {
                _parkedEventId = y.Event.EventId;
                _eventParked.Set();
            }, 
            (x,y,z)=> { }, 
            DefaultData.AdminCredentials).Wait();

            var response = MakePost(_nackLink, _admin);
            Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
        }

        [Test]
        public void should_have_parked_the_event()
        {
            Assert.IsTrue(_eventParked.WaitOne(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(_eventIdToPark, _parkedEventId);
        }
    }

    class when_replaying_parked_message : with_subscription_having_events
    {
        private string _nackLink;
        private AutoResetEvent _eventParked = new AutoResetEvent(false);
        private Guid _eventIdToPark;
        private EventStore.ClientAPI.ResolvedEvent replayedParkedEvent;
        protected override void Given()
        {
            NumberOfEventsToCreate = 1;
            base.Given();

            var json = GetJson<JObject>(
               SubscriptionPath + "/1?embed=rich",
               ContentType.AtomJson,
               _admin);

            Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);

            var _entries = json != null ? json["entries"].ToList() : new List<JToken>();
            _nackLink = _entries[0]["links"][3]["uri"].ToString() + "?action=park";
            _eventIdToPark = Guid.Parse(_entries[0]["eventId"].ToString());

            //Park the message
            var response = MakePost(_nackLink, _admin);
            Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
        }

        protected override void When()
        {
            _connection.ConnectToPersistentSubscription(TestStreamName, GroupName, (x, y) =>
            {
                replayedParkedEvent = y;
                _eventParked.Set();
            },
            (x, y, z) => { },
            DefaultData.AdminCredentials);

            //Replayed parked messages
            var response = MakePost(SubscriptionPath + "/replayParked", _admin);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Test]
        public void should_have_replayed_the_parked_event()
        {
            Assert.IsTrue(_eventParked.WaitOne(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(replayedParkedEvent.Event.EventId, _eventIdToPark);
        }
    }
}
