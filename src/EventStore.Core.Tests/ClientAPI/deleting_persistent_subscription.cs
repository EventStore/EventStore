using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class deleting_existing_persistent_subscription_group_with_permissions : SpecificationWithMiniNode
    {
        private readonly string _stream = Guid.NewGuid().ToString();

        protected override void When()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, "groupname1", false,
                new UserCredentials("admin", "changeit"));
        }

        [Test]
        public void the_delete_of_group_succeeds()
        {
            var result = _conn.DeletePersistentSubscriptionAsync(_stream, "groupname1", new UserCredentials("admin","changeit")).Result;
            Assert.AreEqual(PersistentSubscriptionDeleteStatus.Success, result.Status);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class deleting_persistent_subscription_group_that_doesnt_exist : SpecificationWithMiniNode
    {
        private readonly string _stream = Guid.NewGuid().ToString();

        protected override void When()
        {
        }

        [Test]
        public void the_delete_fails_with_argument_exception()
        {
            try
            {
                _conn.DeletePersistentSubscriptionAsync(_stream, Guid.NewGuid().ToString(), new UserCredentials("admin","changeit")).Wait();
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
    public class deleting_persistent_subscription_group_without_permissions : SpecificationWithMiniNode
    {
        private readonly string _stream = Guid.NewGuid().ToString();

        protected override void When()
        {
        }

        [Test]
        public void the_delete_fails_with_access_denied()
        {
            try
            {
                _conn.DeletePersistentSubscriptionAsync(_stream, Guid.NewGuid().ToString()).Wait();
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