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
        private Exception _caught;

        protected override void When()
        {
            try
            {
                _conn.ConnectToPersistentSubscription("foo",
                    "nonexisting2",
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
        private readonly string _stream = Guid.NewGuid().ToString();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();

        protected override void When()
        {
            _conn.CreatePersistentSubscriptionAsync("agroupname17", _stream , _settings, new UserCredentials("admin", "changeit")).Wait();
            _sub = _conn.ConnectToPersistentSubscription(_stream,
                "agroupname17",
                (sub, e) => Console.Write("appeared"),
                (sub, reason, ex) => {});
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
        private readonly string _stream = "$" + Guid.NewGuid();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();
        protected override void When()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, "agroupname55", _settings,
                new UserCredentials("admin", "changeit")).Wait();
        }

        [Test]
        public void the_subscription_fails_to_connect()
        {
            try
            {
                _conn.ConnectToPersistentSubscription("agroupname55", 
                    _stream,
                    (sub, e) => Console.Write("appeared"),
                    (sub, reason, ex) => {});
                throw new Exception("should have thrown.");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOf<AggregateException>(ex);
                Assert.IsInstanceOf<AccessDeniedException>(ex.InnerException);
            }
        }
    }

    //ALL
    [TestFixture, Category("LongRunning")]
    public class connect_to_non_existing_persistent_all_subscription_with_permissions : SpecificationWithMiniNode
    {
        private Exception _caught;

        protected override void When()
        {
            try
            {
                _conn.ConnectToPersistentSubscriptionForAll("nonexisting2",
                    (sub, e) => Console.Write("appeared"),
                    (sub, reason, ex) =>
                    {
                    }, 
                    new UserCredentials("admin", "changeit"));
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
    public class connect_to_existing_persistent_all_subscription_with_permissions : SpecificationWithMiniNode
    {
        private EventStorePersistentSubscription _sub;
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();
        protected override void When()
        {
            _conn.CreatePersistentSubscriptionForAllAsync("agroupname17", _settings, new UserCredentials("admin", "changeit")).Wait();
            _sub = _conn.ConnectToPersistentSubscriptionForAll("agroupname17",
                (sub, e) => Console.Write("appeared"),
                (sub, reason, ex) => { }, new UserCredentials("admin", "changeit"));
        }

        [Test]
        public void the_subscription_suceeds()
        {
            Assert.IsNotNull(_sub);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_all_subscription_without_permissions : SpecificationWithMiniNode
    {
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();

        protected override void When()
        {
            _conn.CreatePersistentSubscriptionForAllAsync("agroupname55", _settings,
                new UserCredentials("admin", "changeit")).Wait();
        }

        [Test]
        public void the_subscription_fails_to_connect()
        {
            try
            {
                _conn.ConnectToPersistentSubscriptionForAll("agroupname55",
                    (sub, e) => Console.Write("appeared"),
                    (sub, reason, ex) => { });
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
