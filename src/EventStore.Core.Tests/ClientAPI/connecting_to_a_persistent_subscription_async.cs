extern alias GrpcClient;
extern alias GrpcClientPersistent;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests.ClientAPI.Helpers;
using GrpcClientPersistent::EventStore.Client;
using AccessDeniedException = GrpcClient::EventStore.Client.AccessDeniedException;
using EventData = GrpcClient::EventStore.Client.EventData;
using ResolvedEvent = GrpcClient::EventStore.Client.ResolvedEvent;
using StreamPosition = GrpcClient::EventStore.Client.StreamPosition;
using Uuid = GrpcClient::EventStore.Client.Uuid;

namespace EventStore.Core.Tests.ClientAPI {
	extern alias GrpcClient;

	[Category("LongRunning"), Category("ClientAPI")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class connect_to_non_existing_persistent_subscription_with_permissions_async<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private Exception _innerEx;

		protected override async Task When() {
			_innerEx = await AssertEx.ThrowsAsync<PersistentSubscriptionNotFoundException>(() => _conn.ConnectToPersistentSubscriptionAsync(
				"nonexisting2",
				"foo",
				(sub, e) => {
					Console.Write("appeared");
					return Task.CompletedTask;
				},
				(sub, reason, ex) => { }, DefaultData.AdminCredentials));
		}

		[Test]
		public void the_subscription_fails_to_connect_with_argument_exception() {
			Assert.IsInstanceOf<PersistentSubscriptionNotFoundException>(_innerEx);
		}
	}

	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class connect_to_existing_persistent_subscription_with_permissions_async<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private PersistentSubscription _sub;
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.End);

		protected override async Task When() {
			await _conn.CreatePersistentSubscriptionAsync(_stream, "agroupname17", _settings, DefaultData.AdminCredentials)
;
			_sub = await _conn.ConnectToPersistentSubscriptionAsync(_stream,
				"agroupname17",
				(sub, e) => {
					Console.Write("appeared");
					return Task.CompletedTask;
				},
				(sub, reason, ex) => { }, DefaultData.AdminCredentials);
		}

		[Test]
		public void the_subscription_succeeds() {
			Assert.IsNotNull(_sub);
		}
	}

	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class connect_to_existing_persistent_subscription_without_permissions_async<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.End);

		private Exception _innerEx;

		protected override async Task When() {
			await _conn.CreatePersistentSubscriptionAsync(_stream, "agroupname55", _settings,
				DefaultData.AdminCredentials);
			_innerEx = await AssertEx.ThrowsAsync<AccessDeniedException>(() => _conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				"agroupname55",
				(sub, e) => {
					Console.Write("appeared");
					return Task.CompletedTask;
				},
				(sub, reason, ex) => Console.WriteLine("dropped.")));
		}

		[Test]
		public void the_subscription_fails_to_connect_with_access_denied_exception() {
			Assert.IsInstanceOf<AccessDeniedException>(_innerEx);
		}
	}

	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class connect_to_existing_persistent_subscription_with_max_one_client_async<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.End,
				maxSubscriberCount: 1);

		private Exception _innerEx;

		private const string _group = "startinbeginning1";
		private PersistentSubscription _firstConn;

		protected override async Task Given() {
			await base.Given();
			await _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials);
			// First connection
			_firstConn = await _conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				_group,
				(s, e) => s.Ack(e),
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
		}

		protected override async Task When() {
			_innerEx = await AssertEx.ThrowsAsync<MaximumSubscribersReachedException>(() =>
				// Second connection
				_conn.ConnectToPersistentSubscriptionAsync(
					_stream,
					_group,
					(s, e) => s.Ack(e),
					(sub, reason, ex) => { },
					DefaultData.AdminCredentials));
		}

		[Test]
		public void the_first_subscription_connects_successfully() {
			Assert.IsNotNull(_firstConn);
		}

		[Test]
		public void the_second_subscription_throws_maximum_subscribers_reached_exception() {
			Assert.IsInstanceOf<MaximumSubscribersReachedException>(_innerEx);
		}
	}

	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class
		connect_to_existing_persistent_subscription_with_start_from_beginning_and_no_stream_async<TLogFormat, TStreamId> :
			SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.Start);

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private readonly Uuid _id = Uuid.NewUuid();
		private bool _set = false;

		private const string _group = "startinbeginning1";

		protected override async Task Given() {
			await _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials);

			await _conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
		}

		protected override Task When() {
			return _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
				new EventData(_id, "test", Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0]));
		}

		private Task HandleEvent(PersistentSubscription sub, ResolvedEvent resolvedEvent) {
			if (_set)
				return Task.CompletedTask;
			_set = true;
			_firstEvent = resolvedEvent;
			_resetEvent.Set();
			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_event_zero_as_its_first_event() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.AreEqual(0, _firstEvent.Event.EventNumber.ToInt64());
			Assert.AreEqual(_id, _firstEvent.Event.EventId);
		}
	}

	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class
		connect_to_existing_persistent_subscription_with_start_from_two_and_no_stream_async<TLogFormat, TStreamId> :
			SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.FromInt64(2));

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private readonly Uuid _id = Uuid.NewUuid();
		private bool _set = false;

		private const string _group = "startinbeginning1";

		protected override async Task Given() {
			await _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials);
			await _conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
		}

		protected override async Task When() {
			await _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
					new EventData(Uuid.NewUuid(), "test", Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0]));
			await _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
					new EventData(Uuid.NewUuid(), "test", Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0]));
			await _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
				new EventData(_id, "test", Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0]));
		}

		private Task HandleEvent(PersistentSubscription sub, ResolvedEvent resolvedEvent) {
			if (_set)
				return Task.CompletedTask;
			_set = true;
			_firstEvent = resolvedEvent;
			_resetEvent.Set();
			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_event_two_as_its_first_event() {
			Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
			Assert.AreEqual(2, _firstEvent.Event.EventNumber.ToInt64());
			Assert.AreEqual(_id, _firstEvent.Event.EventId);
		}
	}

	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class
		connect_to_existing_persistent_subscription_with_start_from_beginning_and_events_in_it_async<TLogFormat, TStreamId> :
			SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.Start);

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private ResolvedEvent _firstEvent;
		private List<Uuid> _ids = new List<Uuid>();
		private bool _set = false;

		private const string _group = "startinbeginning1";

		protected override async Task Given() {
			await WriteEvents(_conn);
			await _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials);
		}

		private async Task WriteEvents(IEventStoreClient connection) {
			for (int i = 0; i < 10; i++) {
				_ids.Add(Uuid.NewUuid());
				await connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
						new EventData(_ids[i], "test", Encoding.UTF8.GetBytes("{'foo' : 'bar'}"), new byte[0]));
			}
		}

		protected override Task When() {
			return _conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
		}

		private Task HandleEvent(PersistentSubscription sub, ResolvedEvent resolvedEvent) {
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
			Assert.AreEqual(0, _firstEvent.Event.EventNumber.ToInt64());
			Assert.AreEqual(_ids[0], _firstEvent.Event.EventId);
		}
	}

	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class
		connect_to_existing_persistent_subscription_with_start_from_beginning_not_set_and_events_in_it_async<TLogFormat, TStreamId> :
			SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly string _stream = "$" + Guid.NewGuid();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.End);

		private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);

		private const string _group = "startinbeginning1";

		protected override async Task Given() {
			await WriteEvents(_conn);
			await _conn.CreatePersistentSubscriptionAsync(_stream, _group, _settings,
				DefaultData.AdminCredentials);
		}

		private async Task WriteEvents(IEventStoreClient connection) {
			for (int i = 0; i < 10; i++) {
				await connection.AppendToStreamAsync(_stream, ExpectedVersion.Any, DefaultData.AdminCredentials,
						new EventData(Uuid.NewUuid(), "test", Encoding.UTF8.GetBytes("{'foo' : 'bar'}"),
							new byte[0]));
			}
		}

		protected override Task When() {
			return _conn.ConnectToPersistentSubscriptionAsync(
				_stream,
				_group,
				HandleEvent,
				(sub, reason, ex) => { },
				DefaultData.AdminCredentials);
		}

		private Task HandleEvent(PersistentSubscription sub, ResolvedEvent resolvedEvent) {
			_resetEvent.Set();
			return Task.CompletedTask;
		}

		[Test]
		public void the_subscription_gets_no_events() {
			Assert.IsFalse(_resetEvent.WaitOne(TimeSpan.FromSeconds(1)));
		}
	}
}
