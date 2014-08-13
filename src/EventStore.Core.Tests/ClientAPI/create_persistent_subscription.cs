using System;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class create_persistent_subscription_on_existing_stream : SpecificationWithMiniNode
    {
        private PersistentSubscriptionCreateResult _result;
        private readonly string _stream = Guid.NewGuid().ToString();
        protected override void When()
        {
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any,
                new EventData(Guid.NewGuid(), "whatever", true, Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
            _result = _conn.CreatePersistentSubscriptionAsync(_stream, "existing", true, new UserCredentials("admin", "changeit")).Result;
        }

        [Test]
        public void the_completion_succeeds()
        {
            Assert.AreEqual(PersistentSubscriptionCreateStatus.Success, _result.Status);
        }
    }

    //[TestFixture, Category("LongRunning")]
    //public class create_persistent_subscription_without_permissions_results_in_access_denied : SpecificationWithMiniNode
    //{
    //    private PersistentSubscriptionCreateResult _result;

    //    protected override void When()
    //    {
    //        _conn.AppendToStreamAsync("foo", ExpectedVersion.Any,
    //            new EventData(Guid.NewGuid(), "whatever", true, Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
    //        _result = _conn.CreatePersistentSubscriptionAsync("foo", "group", true, null).Result;
    //    }

    //    [Test]
    //    public void the_completion_succeeds()
    //    {
    //        Assert.AreEqual(PersistentSubscriptionCreateStatus.Success, _result.Status);
    //    }
    //}

    [TestFixture, Category("LongRunning")]
    public class create_persistent_subscription_on_non_existing_stream : SpecificationWithMiniNode
    {
        private PersistentSubscriptionCreateResult _result;
        private readonly string _stream = Guid.NewGuid().ToString();
        protected override void When()
        {
            _result = _conn.CreatePersistentSubscriptionAsync(_stream, "nonexistinggroup", true, new UserCredentials("admin", "changeit")).Result;
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
        private readonly string _stream = Guid.NewGuid().ToString();
        protected override void When()
        {
            _result = _conn.CreatePersistentSubscriptionAsync(_stream, "group", true, new UserCredentials("admin", "changeit")).Result;
        }

        [Test]
        public void the_completion_succeeds()
        {
            try
            {
                _result = _conn.CreatePersistentSubscriptionAsync("foo", "group", true, new UserCredentials("admin", "changeit")).Result;
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


    [TestFixture, Category("LongRunning")]
    public class create_persistent_subscription_group_without_permissions : SpecificationWithMiniNode
    {
        private PersistentSubscriptionCreateResult _result;
        private readonly string _stream = Guid.NewGuid().ToString();

        protected override void When()
        {
        }

        [Test]
        public void the_completion_succeeds()
        {
            try
            {
                _result = _conn.CreatePersistentSubscriptionAsync(_stream, "group", true, null).Result;
                throw new Exception("expected exception");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOf(typeof(AggregateException), ex);
                var inner = ex.InnerException;
                Assert.IsInstanceOf(typeof(AccessDeniedException), inner);
            }
        }
    }

}