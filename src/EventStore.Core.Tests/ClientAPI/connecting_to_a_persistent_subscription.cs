using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("LongRunning"), Category("ClientAPI")]
	public class connect_to_non_existing_persistent_subscription_with_permissions : SpecificationWithMiniNode {
		private Exception _caught;

		protected override void When() {
			_caught = Assert.Throws<AggregateException>(() => {
				_conn.ConnectToPersistentSubscription(
					"nonexisting2",
					"foo",
					(sub, e) => {
						Console.Write("appeared");
						return Task.CompletedTask;
					},
					(sub, reason, ex) => { });
				throw new Exception("should have thrown");
			}).InnerException;
		}

		[Test]
		public void the_completion_fails() {
			Assert.IsNotNull(_caught);
		}

		[Test]
		public void the_exception_is_an_argument_exception() {
			Assert.IsInstanceOf<ArgumentException>(_caught.InnerException);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_with_permissions : SpecificationWithMiniNode {
		private EventStorePersistentSubscriptionBase _sub;
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent();

		protected override void When() {
			_conn.CreatePersistentSubscriptionAsync(_stream, "agroupname17", _settings, DefaultData.AdminCredentials)
				.Wait();
			_sub = _conn.ConnectToPersistentSubscription(_stream,
				"agroupname17",
				(sub, e) => {
					Console.Write("appeared");
					return Task.CompletedTask;
				},
				(sub, reason, ex) => { });
		}

		[Test]
		public void the_subscription_suceeds() {
			Assert.IsNotNull(_sub);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_without_permissions : SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent();

		protected override void When() {
			_conn.CreatePersistentSubscriptionAsync(_stream, "agroupname55", _settings,
				DefaultData.AdminCredentials).Wait();
		}

		[Test]
		public void the_subscription_fails_to_connect() {
			try {
				_conn.ConnectToPersistentSubscription(
					_stream,
					"agroupname55",
					(sub, e) => {
						Console.Write("appeared");
						return Task.CompletedTask;
					},
					(sub, reason, ex) => Console.WriteLine("dropped."));
				throw new Exception("should have thrown.");
			} catch (Exception ex) {
				var innerEx = ex.InnerException;
				Assert.IsInstanceOf<AggregateException>(innerEx);
				Assert.IsInstanceOf<AccessDeniedException>(innerEx.InnerException);
			}
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_with_max_one_client : SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent()
			.WithMaxSubscriberCountOf(1);

		private Exception _exception;

		private const string _group = "startinbeginning1";

		protected override void Given() {
			base.Given();
			_conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials).Wait();
			_conn.ConnectToPersistentSubscription(
				_stream,
				_group,
				(s, e) => {
					s.Acknowledge(e);
					return Task.CompletedTask;
				},
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
		}

		protected override void When() {
			_exception = Assert.Throws<AggregateException>(() => {
				_conn.ConnectToPersistentSubscription(
					_stream,
					_group,
					(s, e) => {
						s.Acknowledge(e);
						return Task.CompletedTask;
					},
					(sub, reason, ex) => { },
					DefaultData.AdminCredentials);
				throw new Exception("should have thrown.");
			}).InnerException;
		}

		[Test]
		public void the_second_subscription_fails_to_connect() {
			Assert.IsInstanceOf<AggregateException>(_exception);
			Assert.IsInstanceOf<MaximumSubscribersReachedException>(_exception.InnerException);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_beginning_and_no_stream :
			SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromBeginning();

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private readonly Guid _id = Guid.NewGuid();
		private bool _set = false;

		private const string _group = "startinbeginning1";

		protected override void Given() {
			_conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials).Wait();
			_conn.ConnectToPersistentSubscription(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
		}

		protected override void When() {
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
				new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
		}

		private Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent) {
			if (_set) return Task.CompletedTask;
			_set = true;
			_firstEvent = resolvedEvent;
			_resetEvent.Set();
			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_event_zero_as_its_first_event() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.AreEqual(0, _firstEvent.Event.EventNumber);
			Assert.AreEqual(_id, _firstEvent.Event.EventId);
		}
	}


	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_two_and_no_stream : SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFrom(2);

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private readonly Guid _id = Guid.NewGuid();
		private bool _set = false;

		private const string _group = "startinbeginning1";

		protected override void Given() {
			_conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials).Wait();
			_conn.ConnectToPersistentSubscription(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
		}

		protected override void When() {
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
					new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0]))
				.Wait();
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
					new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0]))
				.Wait();
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
				new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
		}

		private Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent) {
			if (_set) return Task.CompletedTask;
			_set = true;
			_firstEvent = resolvedEvent;
			_resetEvent.Set();
			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_event_two_as_its_first_event() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.AreEqual(2, _firstEvent.Event.EventNumber);
			Assert.AreEqual(_id, _firstEvent.Event.EventId);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_beginning_and_events_in_it :
			SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromBeginning();

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private List<Guid> _ids = new List<Guid>();
		private bool _set = false;

		private const string _group = "startinbeginning1";

		protected override void Given() {
			WriteEvents(_conn);
			_conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials).Wait();
		}

		private void WriteEvents(IEventStoreConnection connection) {
			for (int i = 0; i < 10; i++) {
				_ids.Add(Guid.NewGuid());
				connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
						new EventData(_ids[i], "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0]))
					.Wait();
			}
		}

		protected override void When() {
			_conn.ConnectToPersistentSubscription(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
		}

		private Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent) {
			if (!_set) {
				_set = true;
				_firstEvent = resolvedEvent;
				_resetEvent.Set();
			}

			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_event_zero_as_its_first_event() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.AreEqual(0, _firstEvent.Event.EventNumber);
			Assert.AreEqual(_ids[0], _firstEvent.Event.EventId);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_beginning_not_set_and_events_in_it :
			SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent();

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);

		private const string _group = "startinbeginning1";

		protected override void Given() {
			WriteEvents(_conn);
			_conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials).Wait();
		}

		private void WriteEvents(IEventStoreConnection connection) {
			for (int i = 0; i < 10; i++) {
				connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
						new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"),
							new byte[0]))
					.Wait();
			}
		}

		protected override void When() {
			_conn.ConnectToPersistentSubscription(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
		}

		private Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent) {
			_resetEvent.Set();
			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_no_events() {
			Assert.IsFalse(_resetEvent.WaitOne(TimeSpan.FromSeconds(1)));
		}
	}

	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_beginning_not_set_and_events_in_it_then_event_written :
			SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos();

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private Guid _id;

		private const string _group = "startinbeginning1";

		protected override void Given() {
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

		private void WriteEvents(IEventStoreConnection connection) {
			for (int i = 0; i < 10; i++) {
				connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
						new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"),
							new byte[0]))
					.Wait();
			}
		}

		protected override void When() {
			_id = Guid.NewGuid();
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
				new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
		}

		private Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent) {
			_firstEvent = resolvedEvent;
			_resetEvent.Set();
			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_the_written_event_as_its_first_event() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.IsNotNull(_firstEvent);
			Assert.AreEqual(10, _firstEvent.Event.EventNumber);
			Assert.AreEqual(_id, _firstEvent.Event.EventId);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_x_set_higher_than_x_and_events_in_it_then_event_written :
			SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFrom(11);

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private Guid _id;

		private const string _group = "startinbeginning1";

		protected override void Given() {
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

		private void WriteEvents(IEventStoreConnection connection) {
			for (int i = 0; i < 11; i++) {
				connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
						new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"),
							new byte[0]))
					.Wait();
			}
		}

		protected override void When() {
			_id = Guid.NewGuid();
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
				new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
		}

		private Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent) {
			_firstEvent = resolvedEvent;
			_resetEvent.Set();
			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_the_written_event_as_its_first_event() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.IsNotNull(_firstEvent);
			Assert.AreEqual(11, _firstEvent.Event.EventNumber);
			Assert.AreEqual(_id, _firstEvent.Event.EventId);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class a_nak_in_subscription_handler_in_autoack_mode_drops_the_subscription : SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromBeginning();

		private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
		private Exception _exception;
		private SubscriptionDropReason _reason;

		private const string _group = "naktest";

		protected override void Given() {
			_conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials).Wait();
			_conn.ConnectToPersistentSubscription(
				_stream,
				_group,
				HandleEvent,
				Dropped,
				DefaultData.AdminCredentials);
		}

		private void Dropped(EventStorePersistentSubscriptionBase sub, SubscriptionDropReason reason,
			Exception exception) {
			_exception = exception;
			_reason = reason;
			_resetEvent.Set();
		}

		protected override void When() {
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
					new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0]))
				.Wait();
		}

		private static Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent) {
			throw new Exception("test");
		}

		[Test]
		public void the_subscription_gets_dropped() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(5)));
			Assert.AreEqual(SubscriptionDropReason.EventHandlerException, _reason);
			Assert.AreEqual(typeof(Exception), _exception.GetType());
			Assert.AreEqual("test", _exception.Message);
		}
	}


	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_x_set_and_events_in_it_then_event_written :
			SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFrom(10);

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private Guid _id;

		private const string _group = "startinbeginning1";

		protected override void Given() {
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

		private void WriteEvents(IEventStoreConnection connection) {
			for (int i = 0; i < 10; i++) {
				connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
						new EventData(Guid.NewGuid(), "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"),
							new byte[0]))
					.Wait();
			}
		}

		protected override void When() {
			_id = Guid.NewGuid();
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
				new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
		}

		private Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent) {
			_firstEvent = resolvedEvent;
			_resetEvent.Set();
			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_the_written_event_as_its_first_event() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.IsNotNull(_firstEvent);
			Assert.AreEqual(10, _firstEvent.Event.EventNumber);
			Assert.AreEqual(_id, _firstEvent.Event.EventId);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_x_set_and_events_in_it : SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFrom(4);

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private Guid _id;

		private const string _group = "startinx2";

		protected override void Given() {
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

		private void WriteEvents(IEventStoreConnection connection) {
			for (int i = 0; i < 10; i++) {
				var id = Guid.NewGuid();
				connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
					new EventData(id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
				if (i == 4) _id = id;
			}
		}

		protected override void When() {
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
				new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
		}

		private bool _set = false;

		private Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent) {
			if (_set) return Task.CompletedTask;
			_set = true;
			_firstEvent = resolvedEvent;
			_resetEvent.Set();
			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_the_written_event_as_its_first_event() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.IsNotNull(_firstEvent);
			Assert.AreEqual(4, _firstEvent.Event.EventNumber);
			Assert.AreEqual(_id, _firstEvent.Event.EventId);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class
		connect_to_persistent_subscription_with_link_to_event_with_event_number_greater_than_int_maxvalue :
			ExpectedVersion64Bit.MiniNodeWithExistingRecords {
		private const string StreamName =
			"connect_to_persistent_subscription_with_link_to_event_with_event_number_greater_than_int_maxvalue";

		private const long intMaxValue = (long)int.MaxValue;

		private string _linkedStreamName = "linked-" + StreamName;
		private const string _group = "group-" + StreamName;

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.ResolveLinkTos()
			.StartFromBeginning();

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private bool _set = false;
		private Guid _event1Id;

		public override void WriteTestScenario() {
			var event1 = WriteSingleEvent(StreamName, intMaxValue + 1, new string('.', 3000));
			WriteSingleEvent(StreamName, intMaxValue + 2, new string('.', 3000));
			_event1Id = event1.EventId;
		}

		public override void Given() {
			_store = BuildConnection(Node);
			_store.ConnectAsync().Wait();

			_store.CreatePersistentSubscriptionAsync(_linkedStreamName, _group, _settings,
				DefaultData.AdminCredentials).Wait();
			_store.ConnectToPersistentSubscription(
				_linkedStreamName,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
			_store.AppendToStreamAsync(_linkedStreamName, ExpectedVersion.Any, new EventData(Guid.NewGuid(),
				SystemEventTypes.LinkTo, false, Helper.UTF8NoBom.GetBytes(
					string.Format("{0}@{1}", intMaxValue + 1, StreamName)), null));
		}

		private Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent) {
			if (!_set) {
				_set = true;
				_firstEvent = resolvedEvent;
				_resetEvent.Set();
			}

			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_resolves_the_linked_event_correctly() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.AreEqual(intMaxValue + 1, _firstEvent.Event.EventNumber);
			Assert.AreEqual(_event1Id, _firstEvent.Event.EventId);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_persistent_subscription_with_retries : SpecificationWithMiniNode {
		private readonly string _stream = Guid.NewGuid().ToString("N");

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromBeginning();

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private readonly Guid _id = Guid.NewGuid();
		int? _retryCount;
		private const string _group = "retries";

		protected override void Given() {
			_conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials).Wait();
			_conn.ConnectToPersistentSubscription(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials, autoAck: false);
		}

		protected override void When() {
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
				new EventData(_id, "test", true, Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0])).Wait();
		}

		private Task HandleEvent(EventStorePersistentSubscriptionBase sub, ResolvedEvent resolvedEvent,
			int? retryCount) {
			if (retryCount > 4) {
				_retryCount = retryCount;
				sub.Acknowledge(resolvedEvent);
				_resetEvent.Set();
			} else {
				sub.Fail(resolvedEvent, PersistentSubscriptionNakEventAction.Retry, "Not yet tried enough times");
			}

			return Task.CompletedTask;
		}

		[Test]
		public void events_are_retried_until_success() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.AreEqual(5, _retryCount);
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
	                    DefaultData.AdminCredentials);
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
	            _conn.CreatePersistentSubscriptionForAllAsync("agroupname17", _settings, DefaultData.AdminCredentials).Wait();
	            _sub = _conn.ConnectToPersistentSubscriptionForAll("agroupname17",
	                (sub, e) => Console.Write("appeared"),
	                (sub, reason, ex) => { }, DefaultData.AdminCredentials);
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
	                DefaultData.AdminCredentials).Wait();
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
