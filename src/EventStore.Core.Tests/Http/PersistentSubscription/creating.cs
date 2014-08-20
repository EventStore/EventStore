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
                "/subscriptions/stream/groupname/",
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
    }

}