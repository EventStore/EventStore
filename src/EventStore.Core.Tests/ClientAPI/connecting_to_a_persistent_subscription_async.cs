using EventStore.ClientAPI;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("LongRunning"), Category("ClientAPI")]
	public class connect_to_non_existing_persistent_subscription_with_permissions_async : SpecificationWithMiniNode {
		private Exception _innerEx;

		protected override void When() {
			_innerEx = Assert.Throws<AggregateException>(() => {
				_conn.ConnectToPersistentSubscriptionAsync(
					"nonexisting2",
					"foo",
					(sub, e) => {
						Console.Write("appeared");
						return Task.CompletedTask;
					},
					(sub, reason, ex) => { }).Wait();
			}).InnerException;
		}

		[Test]
		public void the_subscription_fails_to_connect_with_argument_exception() {
			Assert.IsInstanceOf<AggregateException>(_innerEx);
			Assert.IsInstanceOf<ArgumentException>(_innerEx.InnerException);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_with_permissions_async : SpecificationWithMiniNode {
		private EventStorePersistentSubscriptionBase _sub;
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent();

		protected override void When() {
			_conn.CreatePersistentSubscriptionAsync(_stream, "agroupname17", _settings, DefaultData.AdminCredentials)
				.Wait();
			_sub = _conn.ConnectToPersistentSubscriptionAsync(_stream,
				"agroupname17",
				(sub, e) => {
					Console.Write("appeared");
					return Task.CompletedTask;
				},
				(sub, reason, ex) => { }).Result;
		}

		[Test]
		public void the_subscription_suceeds() {
			Assert.IsNotNull(_sub);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_without_permissions_async : SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent();

		private Exception _innerEx;

		protected override void When() {
			_conn.CreatePersistentSubscriptionAsync(_stream, "agroupname55", _settings,
				DefaultData.AdminCredentials).Wait();
			_innerEx = Assert.Throws<AggregateException>(() => {
				_conn.ConnectToPersistentSubscriptionAsync(
					_stream,
					"agroupname55",
					(sub, e) => {
						Console.Write("appeared");
						return Task.CompletedTask;
					},
					(sub, reason, ex) => Console.WriteLine("dropped.")).Wait();
			}).InnerException;
		}

		[Test]
		public void the_subscription_fails_to_connect_with_access_denied_exception() {
			Assert.IsInstanceOf<AggregateException>(_innerEx);
			Assert.IsInstanceOf<AccessDeniedException>(_innerEx.InnerException);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class connect_to_existing_persistent_subscription_with_max_one_client_async : SpecificationWithMiniNode {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent()
			.WithMaxSubscriberCountOf(1);

		private Exception _innerEx;

		private const string _group = "startinbeginning1";
		private EventStorePersistentSubscriptionBase _firstConn;

		protected override void Given() {
			base.Given();
			_conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials).Wait();
			// First connection
			_firstConn = _conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				_group,
				(s, e) => {
					s.Acknowledge(e);
					return Task.CompletedTask;
				},
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials).Result;
		}

		protected override void When() {
			_innerEx = Assert.Throws<AggregateException>(() => {
				// Second connection
				_conn.ConnectToPersistentSubscriptionAsync(
					_stream,
					_group,
					(s, e) => {
						s.Acknowledge(e);
						return Task.CompletedTask;
					},
					(sub, reason, ex) => { },
					DefaultData.AdminCredentials).Wait();
			}).InnerException;
		}

		[Test]
		public void the_first_subscription_connects_successfully() {
			Assert.IsNotNull(_firstConn);
		}

		[Test]
		public void the_second_subscription_throws_maximum_subscribers_reached_exception() {
			Assert.IsInstanceOf<AggregateException>(_innerEx);
			Assert.IsInstanceOf<MaximumSubscribersReachedException>(_innerEx.InnerException);
		}
	}

	[TestFixture, Category("LongRunning")]
	public class
		connect_to_existing_persistent_subscription_with_start_from_beginning_and_no_stream_async :
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

			_conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials).Wait();
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
		connect_to_existing_persistent_subscription_with_start_from_two_and_no_stream_async :
			SpecificationWithMiniNode {
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
			_conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials).Wait();
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
		connect_to_existing_persistent_subscription_with_start_from_beginning_and_events_in_it_async :
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
			_conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials).Wait();
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
		connect_to_existing_persistent_subscription_with_start_from_beginning_not_set_and_events_in_it_async :
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
			_conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials).Wait();
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
}
