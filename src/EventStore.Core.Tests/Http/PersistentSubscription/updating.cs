using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Tests.Http.Users.users;
using NUnit.Framework;

namespace EventStore.Core.Tests.Http.PersistentSubscription
{
    [TestFixture, Category("LongRunning")]
    class when_updating_a_subscription_without_permissions : with_admin_user
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
        }

        protected override void When()
        {
            _response = MakeJsonPost(
                "/subscriptions/stream/groupname337",
                new
                {
                    ResolveLinkTos = true
                }, null);
        }

        [Test]
        public void returns_created_status_code()
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, _response.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_updating_a_non_existent_subscription_without_permissions : with_admin_user
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
        }

        protected override void When()
        {
            _response = MakeJsonPost(
                "/subscriptions/stream/groupname3337",
                new
                {
                    ResolveLinkTos = true
                }, new NetworkCredential("admin", "changeit"));
        }

        [Test]
        public void returns_created_not_found_code()
        {
            Assert.AreEqual(HttpStatusCode.NotFound, _response.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_updating_a_existent_subscription : with_admin_user
    {
        private HttpWebResponse _response;
        private readonly string _groupName = Guid.NewGuid().ToString();
        private SubscriptionDropReason _droppedReason;
        private Exception _exception;
        private const string _stream = "stream";
        private AutoResetEvent _dropped = new AutoResetEvent(false);

        protected override void Given()
        {
            _response = MakeJsonPut(
                string.Format("/subscriptions/{0}/{1}", _stream, _groupName),
                new
                {
                    ResolveLinkTos = true
                }, new NetworkCredential("admin", "changeit"));
            SetupSubscription();
        }

        private void SetupSubscription()
        {
            _connection.ConnectToPersistentSubscription(_groupName, _stream, (x, y) => { },
                (sub, reason, ex) =>
                {
                    _droppedReason = reason;
                    _exception = ex;
                    _dropped.Set();
                }, new UserCredentials("admin", "changeit"));
        }

        protected override void When()
        {
            _response = MakeJsonPost(
                string.Format("/subscriptions/{0}/{1}", _stream, _groupName),
                new
                {
                    ResolveLinkTos = true
                }, new NetworkCredential("admin", "changeit"));
        }

        [Test]
        public void returns_created_ok_code()
        {
            Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
        }

        [Test]
        public void existing_subscriptions_are_dropped()
        {
            Assert.IsTrue(_dropped.WaitOne(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(SubscriptionDropReason.UserInitiated, _droppedReason);
            Assert.IsNull(_exception);
        }

        [Test]
        public void the_location_header_is_set()
        {
            Assert.AreEqual(string.Format("http://{0}/subscriptions/{1}/{2}", _node.HttpEndPoint, _stream, _groupName), _response.Headers["Location"]);
        }
    }
}