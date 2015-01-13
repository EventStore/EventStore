using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
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
                _conn.ConnectToPersistentSubscription(
                    "nonexisting2",
                    "foo",
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
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();

        protected override void When()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, "agroupname17", _settings, DefaultData.AdminCredentials).Wait();
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
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();
        protected override void When()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, "agroupname55", _settings,
                DefaultData.AdminCredentials).Wait();
        }

        [Test]
        public void the_subscription_fails_to_connect()
        {
            try
            {
                _conn.ConnectToPersistentSubscription( 
                    _stream,
                    "agroupname55",
                    (sub, e) => Console.Write("appeared"),
                    (sub, reason, ex) => {Console.WriteLine("dropped.");});
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
    public class connect_to_existing_persistent_subscription_with_max_one_client : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent()
                                                                .WithMaxSubscriberCountOf(1);

        private const string _group = "startinbeginning1";

        protected override void When()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();
            _conn.ConnectToPersistentSubscription(
                _stream,
                _group,
                (s, e) => s.Acknowledge(e),
                (sub, reason, ex) => { },
                DefaultData.AdminCredentials);
        }

        [Test]
        public void the_second_subscription_fails_to_connect()
        {
            try
            {
                _conn.ConnectToPersistentSubscription(
                    _stream,
                    _group,
                    (s, e) => s.Acknowledge(e),
                    (sub, reason, ex) => { },
                    DefaultData.AdminCredentials);
                throw new Exception("should have thrown.");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOf<AggregateException>(ex);
                Assert.IsInstanceOf<Exception>(ex.InnerException);
            }
        }
    }

    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_start_from_beginning_and_no_stream : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromBeginning();

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;
        private readonly Guid _id = Guid.NewGuid();
        private bool _set = false;

        private const string _group = "startinbeginning1";

        protected override void Given()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();
            _conn.ConnectToPersistentSubscription(
             _stream,
             _group,
             HandleEvent,
             (sub, reason, ex) => { },
             DefaultData.AdminCredentials);

        }

        protected override void When()
        {
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();            
        }

        private void HandleEvent(EventStorePersistentSubscription sub, ResolvedEvent resolvedEvent)
        {
            if (_set) return;
            _set = true;
            _firstEvent = resolvedEvent;
            _resetEvent.Set();
        }

        [Test]
        public void the_subscription_gets_event_zero_as_its_first_event()
        {
            Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
            Assert.AreEqual(0, _firstEvent.Event.EventNumber);
            Assert.AreEqual(_id, _firstEvent.Event.EventId);
        }
    }


    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_start_from_two_and_no_stream : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFrom(2);

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;
        private readonly Guid _id = Guid.NewGuid();
        private bool _set = false;

        private const string _group = "startinbeginning1";

        protected override void Given()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();
            _conn.ConnectToPersistentSubscription(
             _stream,
             _group,
             HandleEvent,
             (sub, reason, ex) => { },
             DefaultData.AdminCredentials);

        }

        protected override void When()
        {
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();

        }

        private void HandleEvent(EventStorePersistentSubscription sub, ResolvedEvent resolvedEvent)
        {
            if (_set) return;
            _set = true;
            _firstEvent = resolvedEvent;
            _resetEvent.Set();
        }

        [Test]
        public void the_subscription_gets_event_two_as_its_first_event()
        {
            Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
            Assert.AreEqual(2, _firstEvent.Event.EventNumber);
            Assert.AreEqual(_id, _firstEvent.Event.EventId);
        }
    }
    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_start_from_beginning_and_events_in_it : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromBeginning();

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;
        private List<Guid> _ids = new List<Guid>();
        private bool _set = false;

        private const string _group = "startinbeginning1";

        protected override void Given()
        {
            WriteEvents(_conn);
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();

        }

        private void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 10; i++)
            {
                _ids.Add(Guid.NewGuid());
                connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                    new EventData(_ids[i], "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            }
        }

        protected override void When()
        {
            _conn.ConnectToPersistentSubscription(
                _stream,
                _group,
                HandleEvent,
                (sub, reason, ex) => { },
                DefaultData.AdminCredentials);
        }

        private void HandleEvent(EventStorePersistentSubscription sub, ResolvedEvent resolvedEvent)
        {
            if (!_set)
            {
                _set = true;
                _firstEvent = resolvedEvent;
                _resetEvent.Set();
            }
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
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                                .DoNotResolveLinkTos()
                                                                .StartFromCurrent();

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);

        private const string _group = "startinbeginning1";

        protected override void Given()
        {
            WriteEvents(_conn);
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();
        }

        private void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 10; i++)
            {
                connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                    new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            }
        }

        protected override void When()
        {
            _conn.ConnectToPersistentSubscription(
                _stream,
                _group,
                HandleEvent,
                (sub, reason, ex) => { },
                DefaultData.AdminCredentials);
        }

        private void HandleEvent(EventStorePersistentSubscription sub, ResolvedEvent resolvedEvent)
        {
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
        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
                                                                .DoNotResolveLinkTos();

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;
        private Guid _id;

        private const string _group = "startinbeginning1";

        protected override void Given()
        {
            WriteEvents(_conn);
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();
            _conn.ConnectToPersistentSubscription(
                _stream,
                _group,
                HandleEvent,
                (sub, reason, ex) => { },
                DefaultData.AdminCredentials);

        }

        private void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 10; i++)
            {
                connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                    new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            }
        }

        protected override void When()
        {
            _id = Guid.NewGuid();
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
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

    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_start_from_x_set_higher_than_x_and_events_in_it_then_event_written : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();

        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
            .DoNotResolveLinkTos()
            .StartFrom(11);

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;
        private Guid _id;

        private const string _group = "startinbeginning1";

        protected override void Given()
        {
            WriteEvents(_conn);
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();
            _conn.ConnectToPersistentSubscription(
                _stream,
                _group,
                HandleEvent,
                (sub, reason, ex) => { },
                DefaultData.AdminCredentials);

        }

        private void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 11; i++)
            {
                connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                    new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            }
        }

        protected override void When()
        {
            _id = Guid.NewGuid();
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
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
            Assert.AreEqual(11, _firstEvent.Event.EventNumber);
            Assert.AreEqual(_id, _firstEvent.Event.EventId);
        }
    }

    [TestFixture, Category("LongRunning")]
    public class a_nak_in_subscription_handler_in_autoack_mode_drops_the_subscription : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();

        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
            .DoNotResolveLinkTos()
            .StartFromBeginning();

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private Exception _exception;
        private SubscriptionDropReason _reason;

        private const string _group = "naktest";

        protected override void Given()
        {
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();
            _conn.ConnectToPersistentSubscription(
                _stream,
                _group,
                HandleEvent,
                Dropped,
                DefaultData.AdminCredentials);

        }

        private void Dropped(EventStorePersistentSubscription sub, SubscriptionDropReason reason, Exception exception)
        {
            _exception = exception;
            _reason = reason;
            _resetEvent.Set();
        }

        protected override void When()
        {
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();

        }

        private static void HandleEvent(EventStorePersistentSubscription sub, ResolvedEvent resolvedEvent)
        {
            throw new Exception("test");
        }

        [Test]
        public void the_subscription_gets_dropped()
        {
            Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(SubscriptionDropReason.EventHandlerException, _reason);
            Assert.AreEqual(typeof(Exception), _exception.GetType());
            Assert.AreEqual("test", _exception.Message);
        }

    }


    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_start_from_x_set_and_events_in_it_then_event_written : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();

        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
            .DoNotResolveLinkTos()
            .StartFrom(10);

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;
        private Guid _id;

        private const string _group = "startinbeginning1";

        protected override void Given()
        {
            WriteEvents(_conn);
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();
            _conn.ConnectToPersistentSubscription(
                _stream,
                _group,
                HandleEvent,
                (sub, reason, ex) => { },
                DefaultData.AdminCredentials);

        }

        private void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 10; i++)
            {
                connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                    new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
            }
        }

        protected override void When()
        {
            _id = Guid.NewGuid();
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
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

    [TestFixture, Category("LongRunning")]
    public class connect_to_existing_persistent_subscription_with_start_from_x_set_and_events_in_it : SpecificationWithMiniNode
    {
        private readonly string _stream = "$" + Guid.NewGuid();

        private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
            .DoNotResolveLinkTos()
            .StartFrom(4);

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _firstEvent;
        private Guid _id;
        
        private const string _group = "startinx2";

        protected override void Given()
        {
            WriteEvents(_conn);
            _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
                DefaultData.AdminCredentials).Wait();
            _conn.ConnectToPersistentSubscription(
                _stream,
                _group,
                HandleEvent,
                (sub, reason, ex) => { },
                DefaultData.AdminCredentials);

        }

        private void WriteEvents(IEventStoreConnection connection)
        {
            for (int i = 0; i < 10; i++)
            {
                var id = Guid.NewGuid();
                connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                    new EventData(id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
                if (i == 4) _id = id;
            }
        }

        protected override void When()
        {
            _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
                new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();

        }

        private bool _set = false;
        private void HandleEvent(EventStorePersistentSubscription sub, ResolvedEvent resolvedEvent)
        {
            if (_set) return;
            _set = true;
            _firstEvent = resolvedEvent;
            _resetEvent.Set();
        }

        [Test]
        public void the_subscription_gets_the_written_event_as_its_first_event()
        {
            Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
            Assert.IsNotNull(_firstEvent);
            Assert.AreEqual(4, _firstEvent.Event.EventNumber);
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
