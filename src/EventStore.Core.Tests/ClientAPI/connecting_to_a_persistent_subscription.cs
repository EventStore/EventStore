using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class connect_to_non_existing_persistent_subscription_with_permissions : SpecificationWithMiniNode
    {
        private EventStorePersistentSubscription _sub;
        private Exception _caught;

        protected override void When()
        {
            try
            {
                _sub = _conn.ConnectToPersistentSubscription("foo",
                    "nonexisting",
                    (sub, e) => Console.Write("appeared"),
                    (sub, reason, ex) =>
                    {
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
        public void the_exception_is_an_argument_exception()
        {
            Assert.IsInstanceOf<ArgumentException>(_caught.InnerException);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_permissions : SpecificationWithMiniNode
    {
        private EventStorePersistentSubscription _sub;
        private SubscriptionDropReason _reason;
        private Exception _ex;
        private readonly string _stream = Guid.NewGuid().ToString();

        protected override void When()
        {
            _conn.CreatePersistentSubscriptionAsync("agroupname", _stream , true).Wait();
            _sub = _conn.ConnectToPersistentSubscription(_stream,
                "agroupname",
                (sub, e) => Console.Write("appeared"),
                (sub, reason, ex) =>
                {
                    _reason = reason;
                    _ex = ex;
                });
        }

        [Test]
        public void the_subscription_suceeds()
        {
            Assert.IsNotNull(_sub);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_without_permissions : SpecificationWithMiniNode
    {
        private SubscriptionDropReason _reason;
        private Exception _ex;
        private readonly string _stream = "$" + Guid.NewGuid();

        protected override void When()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, "agroupname", true,
                new UserCredentials("admin", "changeit")).Wait();
        }

        [Test]
        public void the_subscription_fails_to_connect()
        {
            try
            {
                _conn.ConnectToPersistentSubscription("agroupname", 
                    _stream,
                    (sub, e) => Console.Write("appeared"),
                    (sub, reason, ex) =>
                    {
                        _reason = reason;
                        _ex = ex;
                    });
                throw new Exception("should have thrown.");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOf<AggregateException>(ex);
                Assert.IsInstanceOf<AccessDeniedException>(ex.InnerException);
            }
        }
    }
}