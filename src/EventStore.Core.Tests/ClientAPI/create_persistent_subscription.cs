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
        private readonly string _stream = Guid.NewGuid().ToString();
        protected override void When()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, "group32", true, new UserCredentials("admin", "changeit")).Wait();
        }

        [Test]
        public void the_completion_fails_with_invalid_operation_exception()
        {
            try
            {
                _conn.CreatePersistentSubscriptionAsync(_stream, "group32", true, new UserCredentials("admin", "changeit")).Wait();
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
    public class can_create_duplicate_persistent_subscription_group_name_on_different_streams : SpecificationWithMiniNode
    {
        private readonly string _stream = Guid.NewGuid().ToString();
        private PersistentSubscriptionCreateResult _result;
        protected override void When()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, "group3211", true, new UserCredentials("admin", "changeit")).Wait();
            _result = _conn.CreatePersistentSubscriptionAsync("someother" + _stream, "group3211", true, new UserCredentials("admin", "changeit")).Result;
        }

        [Test]
        public void the_completion_succeeds()
        {
            Assert.AreEqual(PersistentSubscriptionCreateStatus.Success, _result.Status);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class create_persistent_subscription_group_without_permissions : SpecificationWithMiniNode
    {
        private readonly string _stream = Guid.NewGuid().ToString();

        protected override void When()
        {
        }

        [Test]
        public void the_completion_succeeds()
        {
            try
            {
                _conn.CreatePersistentSubscriptionAsync(_stream, "group57", true, null).Wait();
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
//ALL

    [TestFixture, Category("LongRunning")]
    public class create_persistent_subscription_on_all : SpecificationWithMiniNode
    {
        private PersistentSubscriptionCreateResult _result;
        protected override void When()
        {
            _result = _conn.CreatePersistentSubscriptionForAllAsync("group", true, new UserCredentials("admin", "changeit")).Result;
        }

        [Test]
        public void the_completion_succeeds()
        {
            Assert.AreEqual(PersistentSubscriptionCreateStatus.Success, _result.Status);
        }
    }


    [TestFixture, Category("LongRunning")]
    public class create_duplicate_persistent_subscription_group_on_all : SpecificationWithMiniNode
    {
        protected override void When()
        {
            _conn.CreatePersistentSubscriptionForAllAsync("group32", true, new UserCredentials("admin", "changeit")).Wait();
        }

        [Test]
        public void the_completion_fails_with_invalid_operation_exception()
        {
            try
            {
                _conn.CreatePersistentSubscriptionForAllAsync("group32", true, new UserCredentials("admin", "changeit")).Wait();
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
    public class create_persistent_subscription_group_on_all_without_permissions : SpecificationWithMiniNode
    {
        protected override void When()
        {
        }

        [Test]
        public void the_completion_succeeds()
        {
            try
            {
                _conn.CreatePersistentSubscriptionForAllAsync("group57", true, null).Wait();
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
