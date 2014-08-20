using System.Net;
using EventStore.Core.Tests.Http.Users.users;
using NUnit.Framework;

namespace EventStore.Core.Tests.Http.PersistentSubscription
{
    [TestFixture, Category("LongRunning")]
    class when_deleting_existing_subscription : with_admin_user
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
            _response = MakeJsonPost(
                "/subscriptions/stream/groupname156",
                new
                {
                    ResolveLinkTos = true
                }, _admin);
        }

        protected override void When()
        {
            var req = CreateRequest("/subscriptions/stream/groupname156", "DELETE", _admin);
            _response = GetRequestResponse(req);
        }

        [Test]
        public void returns_ok_status_code()
        {
            Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
        }
    }

    [TestFixture, Category("LongRunning")]
    class when_deleting_existing_subscription_without_permissions : with_admin_user
    {
        private HttpWebResponse _response;

        protected override void Given()
        {
            _response = MakeJsonPost(
                "/subscriptions/stream/groupname156",
                new
                {
                    ResolveLinkTos = true
                }, _admin);
        }

        protected override void When()
        {
            var req = CreateRequest("/subscriptions/stream/groupname156", "DELETE");
            _response = GetRequestResponse(req);
        }

        [Test]
        public void returns_unauthorized_status_code()
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, _response.StatusCode);
        }
    }
    [TestFixture, Category("LongRunning")]
    class when_deleting_non_existing_subscription : with_admin_user
    {
        private HttpWebResponse _response;

        protected override void Given()
        {

        }

        protected override void When()
        {
            var req = CreateRequest("/subscriptions/stream/groupname158", "DELETE", _admin);
            _response = GetRequestResponse(req);
        }

        [Test]
        public void returns_ok_status_code()
        {
            Assert.AreEqual(HttpStatusCode.NotFound, _response.StatusCode);
        }
    }

}