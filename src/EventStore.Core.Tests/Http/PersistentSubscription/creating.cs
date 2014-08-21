using System.Net;
using EventStore.Core.Tests.Http.Users.users;
using NUnit.Framework;

namespace EventStore.Core.Tests.Http.PersistentSubscription
{
    [TestFixture, Category("LongRunning")]
    class when_creating_a_subscription : with_admin_user
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
        }

        protected override void When()
        {
            _response = MakeJsonPost(
                "/subscriptions/stream/groupname334",
                new
                {
                    ResolveLinkTos = true
                }, _admin);
        }

        [Test]
        public void returns_created_status_code()
        {
            Assert.AreEqual(HttpStatusCode.Created, _response.StatusCode);
        }

        [Test]
        public void returns_location_header()
        {
            Assert.AreEqual("http://" + _node.HttpEndPoint + "/subscriptions/stream/groupname334",_response.Headers["location"]);
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_creating_a_subscription_without_permissions : with_admin_user
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
    class when_creating_a_duplicate_subscription : with_admin_user
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
            _response = MakeJsonPost(
                "/subscriptions/stream/groupname453",
                new
                {
                    ResolveLinkTos = true
                }, _admin);
        }

        protected override void When()
        {
            _response = MakeJsonPost(
                "/subscriptions/stream/groupname453",
                new
                {
                    ResolveLinkTos = true
                }, _admin);
        }

        [Test]
        public void returns_created_status_code()
        {
            Assert.AreEqual(HttpStatusCode.Conflict, _response.StatusCode);
        }
    }
}