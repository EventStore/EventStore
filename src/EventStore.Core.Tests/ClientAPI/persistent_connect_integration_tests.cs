extern alias GrpcClient;
extern alias GrpcClientPersistent;
extern alias GrpcClientStreams;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests.ClientAPI.Helpers;
using GrpcClientPersistent::EventStore.Client;
using NUnit.Framework;
using EventData = GrpcClient::EventStore.Client.EventData;
using ResolvedEvent = GrpcClient::EventStore.Client.ResolvedEvent;
using StreamPosition = GrpcClient::EventStore.Client.StreamPosition;
using SystemEventTypes = GrpcClientStreams::EventStore.Client.SystemEventTypes;
using Uuid = GrpcClient::EventStore.Client.Uuid;

namespace EventStore.Core.Tests.ClientAPI {
	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class happy_case_writing_and_subscribing_to_normal_events_manual_ack<TLogFormat, TStreamId>
		: SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private const int BufferCount = 10;
		private const int EventWriteCount = BufferCount * 2;

		private readonly ManualResetEvent _eventsReceived = new ManualResetEvent(false);
		private int _eventReceivedCount;

		protected override Task When() => Task.WhenAll(_node.Started, _node.AdminUserCreated);

		[Test]
		public async Task Test() {
			var streamName = Guid.NewGuid().ToString();
			var groupName = Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.End, resolveLinkTos: true);

			await _conn.CreatePersistentSubscriptionAsync(streamName, groupName, settings, DefaultData.AdminCredentials);
			await _conn.ConnectToPersistentSubscriptionAsync(streamName, groupName,
				async (subscription, resolvedEvent) => {
					await subscription.Ack(resolvedEvent);

					if (Interlocked.Increment(ref _eventReceivedCount) == EventWriteCount) {
						_eventsReceived.Set();
					}
				},
				(sub, reason, exception) =>
					Console.WriteLine("Subscription dropped (reason:{0}, exception:{1}).", reason, exception),
				bufferSize: 10, autoAck: false, userCredentials: DefaultData.AdminCredentials);

			for (var i = 0; i < EventWriteCount; i++) {
				var eventData = new EventData(Uuid.NewUuid(), "SomeEvent", new byte[0], new byte[0]);

				await _conn.AppendToStreamAsync(streamName, ExpectedVersion.Any, DefaultData.AdminCredentials, eventData);
			}

			if (!_eventsReceived.WaitOne(TimeSpan.FromSeconds(5))) {
				throw new Exception("Timed out waiting for events.");
			}
		}
	}


	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class happy_case_writing_and_subscribing_to_normal_events_auto_ack<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private const int BufferCount = 10;
		private const int EventWriteCount = BufferCount * 2;

		private readonly ManualResetEvent _eventsReceived = new ManualResetEvent(false);
		private int _eventReceivedCount;

		protected override Task When() => Task.WhenAll(_node.Started, _node.AdminUserCreated);

		[Test]
		public async Task Test() {
			var streamName = Guid.NewGuid().ToString();
			var groupName = Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.End, resolveLinkTos: true);
			await _conn.CreatePersistentSubscriptionAsync(streamName, groupName, settings, DefaultData.AdminCredentials)
;
			await _conn.ConnectToPersistentSubscriptionAsync(streamName, groupName,
				(subscription, resolvedEvent) => {
					if (Interlocked.Increment(ref _eventReceivedCount) == EventWriteCount) {
						_eventsReceived.Set();
					}

					return Task.CompletedTask;
				},
				(sub, reason, exception) =>
					Console.WriteLine("Subscription dropped (reason:{0}, exception:{1}).", reason, exception),
				userCredentials: DefaultData.AdminCredentials);

			for (var i = 0; i < EventWriteCount; i++) {
				var eventData = new EventData(Uuid.NewUuid(), "SomeEvent", new byte[0], new byte[0]);

				await _conn.AppendToStreamAsync(streamName, ExpectedVersion.Any, DefaultData.AdminCredentials, eventData);
			}

			if (!_eventsReceived.WaitOne(TimeSpan.FromSeconds(15))) {
				throw new Exception($"Timed out waiting for events, received {_eventReceivedCount} event(s).");
			}
		}
	}


	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class happy_case_catching_up_to_normal_events_auto_ack<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private const int BufferCount = 10;
		private const int EventWriteCount = BufferCount * 2;

		private readonly ManualResetEvent _eventsReceived = new ManualResetEvent(false);
		private int _eventReceivedCount;

		protected override Task When() => Task.WhenAll(_node.Started, _node.AdminUserCreated);


		[Test]
		public async Task Test() {
			var streamName = Guid.NewGuid().ToString();
			var groupName = Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.Start, resolveLinkTos: true);
			for (var i = 0; i < EventWriteCount; i++) {
				var eventData = new EventData(Uuid.NewUuid(), "SomeEvent", new byte[0], new byte[0]);

				await _conn.AppendToStreamAsync(streamName, ExpectedVersion.Any, DefaultData.AdminCredentials, eventData);
			}

			await _conn.CreatePersistentSubscriptionAsync(streamName, groupName, settings, DefaultData.AdminCredentials);
			await _conn.ConnectToPersistentSubscriptionAsync(streamName, groupName,
				(subscription, resolvedEvent) => {
					if (Interlocked.Increment(ref _eventReceivedCount) == EventWriteCount) {
						_eventsReceived.Set();
					}

					return Task.CompletedTask;
				},
				(sub, reason, exception) => {
					Console.WriteLine($"Subscription dropped (reason:{reason}, exception:{exception}).");
				},
				userCredentials: DefaultData.AdminCredentials,
				autoAck: true);

			if (!_eventsReceived.WaitOne(TimeSpan.FromSeconds(5))) {
				throw new Exception("Timed out waiting for events.");
			}
		}
	}


	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class happy_case_catching_up_to_normal_events_manual_ack<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private const int BufferCount = 10;
		private const int EventWriteCount = BufferCount * 2;

		private readonly ManualResetEvent _eventsReceived = new ManualResetEvent(false);
		private int _eventReceivedCount;

		protected override Task When() => Task.WhenAll(_node.Started, _node.AdminUserCreated);


		[Test]
		public async Task Test() {
			var streamName = Guid.NewGuid().ToString();
			var groupName = Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.Start, resolveLinkTos: true);
			for (var i = 0; i < EventWriteCount; i++) {
				var eventData = new EventData(Uuid.NewUuid(), "SomeEvent", new byte[0], new byte[0]);

				await _conn.AppendToStreamAsync(streamName, ExpectedVersion.Any, DefaultData.AdminCredentials, eventData);
			}

			await _conn.CreatePersistentSubscriptionAsync(streamName, groupName, settings, DefaultData.AdminCredentials);
			await _conn.ConnectToPersistentSubscriptionAsync(streamName, groupName,
				async (subscription, resolvedEvent) => {
					await subscription.Ack(resolvedEvent);
					if (Interlocked.Increment(ref _eventReceivedCount) == EventWriteCount) {
						_eventsReceived.Set();
					}
				},
				(sub, reason, exception) =>
					Console.WriteLine("Subscription dropped (reason:{0}, exception:{1}).", reason, exception),
				userCredentials: DefaultData.AdminCredentials,
				autoAck: false);

			if (!_eventsReceived.WaitOne(TimeSpan.FromSeconds(5))) {
				throw new Exception("Timed out waiting for events.");
			}
		}
	}


	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class happy_case_catching_up_to_link_to_events_manual_ack<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private const int BufferCount = 10;
		private const int EventWriteCount = BufferCount * 2;

		private readonly ManualResetEvent _eventsReceived = new ManualResetEvent(false);
		private int _eventReceivedCount;

		protected override Task When() => Task.WhenAll(_node.Started, _node.AdminUserCreated);

		[Test]
		public async Task Test() {
			var streamName = Guid.NewGuid().ToString();
			var groupName = Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.Start, resolveLinkTos: true);
			for (var i = 0; i < EventWriteCount; i++) {
				var eventData = new EventData(Uuid.NewUuid(), "SomeEvent", new byte[0], new byte[0]);
				await _conn.AppendToStreamAsync(streamName + "original", ExpectedVersion.Any, DefaultData.AdminCredentials,
					eventData);
			}

			await _conn.CreatePersistentSubscriptionAsync(streamName, groupName, settings, DefaultData.AdminCredentials);
			await _conn.ConnectToPersistentSubscriptionAsync(streamName, groupName,
				async (subscription, resolvedEvent) => {
					await subscription.Ack(resolvedEvent);
					if (Interlocked.Increment(ref _eventReceivedCount) == EventWriteCount) {
						_eventsReceived.Set();
					}
				},
				(sub, reason, exception) =>
					Console.WriteLine("Subscription dropped (reason:{0}, exception:{1}).", reason, exception),
				userCredentials: DefaultData.AdminCredentials,
				autoAck: false);
			for (var i = 0; i < EventWriteCount; i++) {
				var eventData = new EventData(Uuid.NewUuid(), SystemEventTypes.LinkTo,
					Encoding.UTF8.GetBytes(i + "@" + streamName + "original"), null);
				await _conn.AppendToStreamAsync(streamName, ExpectedVersion.Any, DefaultData.AdminCredentials, eventData);
			}

			if (!_eventsReceived.WaitOne(TimeSpan.FromSeconds(5))) {
				throw new Exception("Timed out waiting for events.");
			}
		}
	}


	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class happy_case_catching_up_to_link_to_events_auto_ack<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private const int BufferCount = 10;
		private const int EventWriteCount = BufferCount * 2;

		private readonly ManualResetEvent _eventsReceived = new ManualResetEvent(false);
		private int _eventReceivedCount;

		protected override Task When() => Task.WhenAll(_node.Started, _node.AdminUserCreated);

		[Test]
		public async Task Test() {
			var streamName = Guid.NewGuid().ToString();
			var groupName = Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.Start, resolveLinkTos: true);
			for (var i = 0; i < EventWriteCount; i++) {
				var eventData = new EventData(Uuid.NewUuid(), "SomeEvent", new byte[0], new byte[0]);
				await _conn.AppendToStreamAsync(streamName + "original", ExpectedVersion.Any, DefaultData.AdminCredentials,
					eventData);
			}

			await _conn.CreatePersistentSubscriptionAsync(streamName, groupName, settings, DefaultData.AdminCredentials);
			await _conn.ConnectToPersistentSubscriptionAsync(streamName, groupName,
				(subscription, resolvedEvent) => {
					if (Interlocked.Increment(ref _eventReceivedCount) == EventWriteCount) {
						_eventsReceived.Set();
					}

					return Task.CompletedTask;
				},
				(sub, reason, exception) =>
					Console.WriteLine("Subscription dropped (reason:{0}, exception:{1}).", reason, exception),
				userCredentials: DefaultData.AdminCredentials,
				autoAck: true);
			for (var i = 0; i < EventWriteCount; i++) {
				var eventData = new EventData(Uuid.NewUuid(), SystemEventTypes.LinkTo,
					Encoding.UTF8.GetBytes(i + "@" + streamName + "original"), null);
				await _conn.AppendToStreamAsync(streamName, ExpectedVersion.Any, DefaultData.AdminCredentials, eventData);
			}

			if (!_eventsReceived.WaitOne(TimeSpan.FromSeconds(5))) {
				throw new Exception("Timed out waiting for events.");
			}
		}
	}

	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_writing_and_subscribing_to_normal_events_manual_nack<TLogFormat, TStreamId>
		: SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private const int BufferCount = 10;
		private const int EventWriteCount = BufferCount * 2;

		private readonly ManualResetEvent _eventsReceived = new ManualResetEvent(false);
		private int _eventReceivedCount;

		protected override Task When() => Task.WhenAll(_node.Started, _node.AdminUserCreated);

		[Test]
		public async Task Test() {
			var streamName = Guid.NewGuid().ToString();
			var groupName = Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.End, resolveLinkTos: true);

			await _conn.CreatePersistentSubscriptionAsync(streamName, groupName, settings, DefaultData.AdminCredentials);
			await _conn.ConnectToPersistentSubscriptionAsync(streamName, groupName,
				async (subscription, resolvedEvent) => {
					await subscription.Nack(PersistentSubscriptionNakEventAction.Park, "fail", resolvedEvent);

					if (Interlocked.Increment(ref _eventReceivedCount) == EventWriteCount) {
						_eventsReceived.Set();
					}
				},
				(sub, reason, exception) =>
					Console.WriteLine("Subscription dropped (reason:{0}, exception:{1}).", reason, exception),
				bufferSize: 10, autoAck: false, userCredentials: DefaultData.AdminCredentials);

			for (var i = 0; i < EventWriteCount; i++) {
				var eventData = new EventData(Uuid.NewUuid(), "SomeEvent", new byte[0], new byte[0]);

				await _conn.AppendToStreamAsync(streamName, ExpectedVersion.Any, DefaultData.AdminCredentials, eventData);
			}

			if (!_eventsReceived.WaitOne(TimeSpan.FromSeconds(5))) {
				throw new Exception("Timed out waiting for events.");
			}
		}
	}

	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_connection_drops_messages_that_have_run_out_of_retries_are_not_retried<TLogFormat, TStreamId>
		: SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly TaskCompletionSource<bool> _subscriptionDropped = new TaskCompletionSource<bool>();
		private readonly TaskCompletionSource<bool> _eventReceived = new TaskCompletionSource<bool>();
		private ResolvedEvent _receivedEvent;

		protected override Task When() => Task.WhenAll(_node.Started, _node.AdminUserCreated);

		[Test]
		[Retry(10)]
		public void Test() => Assert.DoesNotThrowAsync(async () => {
			await CloseConnectionAndWait(_conn);
			_conn = BuildConnection(_node);
			await _conn.ConnectAsync();
			
			var streamName = Guid.NewGuid().ToString();
			var groupName = Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.End, maxRetryCount: 0);

			await _conn.CreatePersistentSubscriptionAsync(streamName, groupName, settings, DefaultData.AdminCredentials);
			await _conn.ConnectToPersistentSubscriptionAsync(streamName, groupName, async (subscription, resolvedEvent) => {
					await CloseConnectionAndWait(_conn);
				},
				(sub, reason, exception) => {
					Console.WriteLine("Subscription dropped (reason:{0}, exception:{1}). @ {2}", reason, exception, DateTime.Now);
					_subscriptionDropped.TrySetResult(true);
				},
				bufferSize: 10, autoAck: false, userCredentials: DefaultData.AdminCredentials);

			var parkedEventData = new EventData(Uuid.NewUuid(), "SomeEvent", new byte[0], new byte[0]);
			await _conn.AppendToStreamAsync(streamName, ExpectedVersion.Any, DefaultData.AdminCredentials, parkedEventData);

			await _subscriptionDropped.Task.WithTimeout();

			_conn = BuildConnection(_node);
			await _conn.ConnectAsync();

			await _conn.ConnectToPersistentSubscriptionAsync(streamName, groupName, async (subscription, resolvedEvent) => {
				await subscription.Ack(resolvedEvent);
				_receivedEvent = resolvedEvent;
				_eventReceived.TrySetResult(true);
			}, (sub, reason, exception) => {
				Console.WriteLine("Second Subscription dropped (reason:{0}, exception:{1}).", reason, exception);
			}, bufferSize: 10, autoAck: false, userCredentials: DefaultData.AdminCredentials);

			// Ensure we only get the new event, not the previous one
			var newEventData = new EventData(Uuid.NewUuid(), "SomeEvent", new byte[0], new byte[0]);
			await _conn.AppendToStreamAsync(streamName, ExpectedVersion.Any, DefaultData.AdminCredentials, newEventData);

			await _eventReceived.Task.WithTimeout();
			Assert.AreEqual(newEventData.EventId, _receivedEvent.Event.EventId);
		});
	}
}
