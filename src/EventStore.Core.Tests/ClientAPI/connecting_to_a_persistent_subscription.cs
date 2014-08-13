using System;
using System.Text;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class connect_to_non_existing_persistent_subscription : SpecificationWithMiniNode
    {
        private EventStorePersistentSubscription _sub;
        private SubscriptionDropReason _reason;
        private Exception _ex;
        private Exception _caught;

        protected override void When()
        {
            try
            {
                _sub = _conn.ConnectToPersistentSubscription("nonexisting",
                    "foo",
                    (sub, e) => Console.Write("appeared"),
                    (sub, reason, ex) =>
                    {
                        _reason = reason;
                        _ex = ex;
                    });
                throw new Exception("should have thrown");
            }
            catch (Exception ex)
            {
                _caught = ex;
            }
        }

        [Test]
        public void the_completion_fails()
        {
            Assert.IsNotNull(_caught);
        }

        [Test]
        public void the_exception_is_a_not_found_exception()
        {
            
        }
    }
}