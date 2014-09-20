using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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


    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_start_from_beginning_and_events_in_it : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromBeginning();

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;
        private List<Guid> _ids = new List<Guid>();

        private const string _group = "startinbeginning1";

        protected override void Given()
        {
            WriteEvents(_conn);
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                new UserCredentials("admin", "changeit")).Wait();

        }

        private void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 10; i++)
            {
                _ids.Add(Guid.NewGuid());
                connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, new UserCredentials("admin", "changeit"),
                    new EventData(_ids[i], "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            }
        }

        protected override void When()
        {
            _conn.ConnectToPersistentSubscription(_group,
                _stream,
                HandleEvent,
                (sub, reason, ex) => { },
                userCredentials: new UserCredentials("admin", "changeit"));
        }

        private void HandleEvent(EventStorePersistentSubscription sub, ResolvedEvent resolvedEvent)
        {
            _firstEvent = resolvedEvent;
            _resetEvent.Set();
        }

        [Test]
        public void the_subscription_gets_event_zero_as_its_first_event()
        {
            Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
            Assert.AreEqual(0, _firstEvent.Event.EventNumber);
            Assert.AreEqual(_ids[0], _firstEvent.Event.EventId);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_start_from_beginning_not_set_and_events_in_it : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;

        private const string _group = "startinbeginning1";

        protected override void Given()
        {
            WriteEvents(_conn);
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                new UserCredentials("admin", "changeit")).Wait();
        }

        private void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 10; i++)
            {
                connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, new UserCredentials("admin", "changeit"),
                    new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            }
        }

        protected override void When()
        {
            _conn.ConnectToPersistentSubscription(_group,
                _stream,
                HandleEvent,
                (sub, reason, ex) => { },
                userCredentials: new UserCredentials("admin", "changeit"));
        }

        private void HandleEvent(EventStorePersistentSubscription sub, ResolvedEvent resolvedEvent)
        {
            _firstEvent = resolvedEvent;
            _resetEvent.Set();
        }

        [Test]
        public void the_subscription_gets_no_events()
        {
            Assert.IsFalse(_resetEvent.WaitOne(TimeSpan.FromSeconds(1)));
        }
    }

    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_start_from_beginning_not_set_and_events_in_it_then_event_written : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettingsBuilder.Create()
                                                                .DoNotResolveLinkTos();

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;
        private Guid _id;

        private const string _group = "startinbeginning1";

        protected override void Given()
        {
            WriteEvents(_conn);
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                new UserCredentials("admin", "changeit")).Wait();
            _conn.ConnectToPersistentSubscription(_group,
                _stream,
                HandleEvent,
                (sub, reason, ex) => { },
                userCredentials: new UserCredentials("admin", "changeit"));

        }

        private void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 10; i++)
            {
                connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, new UserCredentials("admin", "changeit"),
                    new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            }
        }

        protected override void When()
        {
            _id = Guid.NewGuid();
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, new UserCredentials("admin", "changeit"),
                new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();

        }

        private void HandleEvent(EventStorePersistentSubscription sub, ResolvedEvent resolvedEvent)
        {
            _firstEvent = resolvedEvent;
            _resetEvent.Set();
        }

        [Test]
        public void the_subscription_gets_the_written_event_as_its_first_event()
        {
            Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
            Assert.IsNotNull(_firstEvent);
            Assert.AreEqual(10, _firstEvent.Event.EventNumber);
            Assert.AreEqual(_id, _firstEvent.Event.EventId);
        }
    }



    //ALL

/*

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
*/

}
