using System;
using System.Text;
using EventStore.ClientAPI;
using EventStore.Core.Tests.Services.VNode;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class create_persistent_subscription_on_existing_stream : SpecificationWithMiniNode
    {
        private PersistentSubscriptionCreateResult _result;

        protected override void When()
        {
            _conn.AppendToStreamAsync("foo", ExpectedVersion.Any,
                new EventData(Guid.NewGuid(), "whatever", true, Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
            _result = _conn.CreatePersistentSubscriptionAsync("foo", "group", true, null).Result;
        }

        [Test]
        public void the_completion_succeeds()
        {
            Assert.AreEqual(PersistentSubscriptionCreateStatus.Success, _result.Status);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class create_persistent_subscription_on_non_existing_stream : SpecificationWithMiniNode
    {
        private PersistentSubscriptionCreateResult _result;

        protected override void When()
        {
            _result = _conn.CreatePersistentSubscriptionAsync("foo", "group", true, null).Result;
        }

        [Test]
        public void the_completion_succeeds()
        {
            Assert.AreEqual(PersistentSubscriptionCreateStatus.Success, _result.Status);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class create_duplicate_persistent_subscription_group : SpecificationWithMiniNode
    {
        private PersistentSubscriptionCreateResult _result;

        protected override void When()
        {
            _result = _conn.CreatePersistentSubscriptionAsync("foo", "group", true, null).Result;
        }

        [Test]
        public void the_completion_succeeds()
        {
            try
            {
                _result = _conn.CreatePersistentSubscriptionAsync("foo", "group", true, null).Result;
                throw new Exception("expected exception");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOf(typeof(AggregateException), ex);
                var inner = ex.InnerException;
                Assert.IsInstanceOf(typeof(InvalidOperationException), inner);
            }
        }
    }
}