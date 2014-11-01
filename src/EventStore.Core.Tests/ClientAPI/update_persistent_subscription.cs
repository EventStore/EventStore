using System;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class update_existing_persistent_subscription : SpecificationWithMiniNode
    {
        private PersistentSubscriptionUpdateResult _result;
        private readonly string _stream = Guid.NewGuid().ToString();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();

        protected override void Given()
        {
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any,
                new EventData(Guid.NewGuid(), "whatever", true, Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
             var res = _conn.CreatePersistentSubscriptionAsync(_stream, "existing", _settings, new UserCredentials("admin", "changeit")).Result;
        }

        protected override void When()
        {
            _result = _conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings, new UserCredentials("admin", "changeit")).Result;
        }

        [Test]
        public void the_completion_succeeds()
        {
            Assert.AreEqual(PersistentSubscriptionUpdateStatus.Success, _result.Status);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class update_existing_persistent_subscription_with_subscribers : SpecificationWithMiniNode
    {
        private PersistentSubscriptionUpdateResult _result;
        private readonly string _stream = Guid.NewGuid().ToString();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();
        private AutoResetEvent _dropped = new AutoResetEvent(false);
        private EventStorePersistentSubscription _sub;
        private SubscriptionDropReason _reason;
        private Exception _exception;

        protected override void Given()
        {
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any,
                new EventData(Guid.NewGuid(), "whatever", true, Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
            var res = _conn.CreatePersistentSubscriptionAsync(_stream, "existing", _settings, new UserCredentials("admin", "changeit")).Result;
            _sub = _conn.ConnectToPersistentSubscription("existing", _stream, (x, y) => { },
                (sub, reason, ex) =>
                {
                    _dropped.Set();
                    _reason = reason;
                    _exception = ex;
                });
        }

        protected override void When()
        {
            _result = _conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings, new UserCredentials("admin", "changeit")).Result;
        }

        [Test]
        public void the_completion_succeeds()
        {
            Assert.AreEqual(PersistentSubscriptionUpdateStatus.Success, _result.Status);
        }

        [Test]
        public void existing_subscriptions_are_dropped()
        {
            Assert.IsTrue(_dropped.WaitOne(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(SubscriptionDropReason.UserInitiated, _reason);
            Assert.IsNull(_exception);
        }

    }



    [TestFixture, Category("LongRunning")]
    public class update_non_existing_persistent_subscription : SpecificationWithMiniNode
    {
        private PersistentSubscriptionUpdateResult _result;
        private readonly string _stream = Guid.NewGuid().ToString();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();

        protected override void When()
        {
            
        }

        [Test]
        public void the_completion_fails_with_not_found()
        {
            try
            {
                _result =
                    _conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings,
                        new UserCredentials("admin", "changeit")).Result;
                Assert.Fail("should have thrown");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOf<AggregateException>(ex);
                Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException);
            }
        }
    }

    [TestFixture, Category("LongRunning")]
    public class update_existing_persistent_subscription_without_permissions : SpecificationWithMiniNode
    {
        private PersistentSubscriptionUpdateResult _result;
        private readonly string _stream = Guid.NewGuid().ToString();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();

        protected override void When()
        {
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any,
                new EventData(Guid.NewGuid(), "whatever", true, Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
            var res = _conn.CreatePersistentSubscriptionAsync(_stream, "existing", _settings, new UserCredentials("admin", "changeit")).Result;

        }

        [Test]
        public void the_completion_fails_with_access_denied()
        {
            try
            {
                _result =
                    _conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings, null).Result;
                Assert.Fail("should have thrown");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOf<AggregateException>(ex);
                Assert.IsInstanceOf<AccessDeniedException>(ex.InnerException);
            }
        }
    }

}