using System;
using System.Net;
using System.Text.RegularExpressions;
using EventStore.Core.Tests.Http.Users.users;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;
using EventStore.Transport.Http;

// ReSharper disable InconsistentNaming

namespace EventStore.Core.Tests.Http.PersistentSubscription
{
    class when_getting_a_message_to_nack : with_subscription_having_events
    {
        private JObject _response;
        protected override void When()
        {
            _response = GetJson<JObject>(
                           SubscriptionPath + "/messages?count=1",
                           ContentType.Any,
                           _admin);
        }

        [Test]
        public void can_nack()
        {
            //TODO: CLC Add links into returned messages
            var id = Guid.Parse(((JArray)_response["events"])[0]["event"]["eventId"].ToString());
            var path = SubscriptionPath + "/messages/" + id + "/nack";
            ///subscriptions/{stream}/{subscription}/messages/{messageid}/nack
            var response = MakePost(path, _admin);

            Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
        }
    }

    class when_getting_n_messages_to_nack : with_subscription_having_events
    {
        private HttpWebResponse _response;
        protected override void When()
        {

            var json = GetJson<JObject>(
                           SubscriptionPath + "/messages?count=" + Events.Count,
                           ContentType.Any,
                           _admin);
            //TODO: CLC Add links into returned messages
            StringBuilder path = new StringBuilder(SubscriptionPath + "//nack?ids=");
            //"/subscriptions/{stream}/{subscription}/nack?ids={messageids}"


            for (int i = 0; i < Events.Count; i++)
            {
                var id = Guid.Parse(((JArray)json["events"])[i]["event"]["eventId"].ToString());
                path.AppendFormat("{0},", id.ToString());
            }

            _response = MakePost(path.ToString().TrimEnd(new[] { ',' }), _admin);
        }
        //todo: add test for rejecting duplicate guids
        [Test]
        public void can_nack_n_messages()
        {
            Assert.AreEqual(HttpStatusCode.Accepted, _response.StatusCode);
        }
    }
}