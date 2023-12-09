﻿extern alias GrpcClient;
extern alias GrpcClientPersistent;
extern alias GrpcClientStreams;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests.ClientAPI.Helpers;
using GrpcClientPersistent::EventStore.Client;
using NUnit.Framework;
using AccessDeniedException = GrpcClient::EventStore.Client.AccessDeniedException;
using EventData = GrpcClient::EventStore.Client.EventData;
using StreamPosition = GrpcClient::EventStore.Client.StreamPosition;
using SubscriptionDroppedReason = GrpcClient::EventStore.Client.SubscriptionDroppedReason;
using Uuid = GrpcClient::EventStore.Client.Uuid;

namespace EventStore.Core.Tests.ClientAPI {
	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class update_existing_persistent_subscription<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.End);

		protected override async Task Given() {
			await _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any,
				new EventData(Uuid.NewUuid(), "whatever", Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
			await _conn.CreatePersistentSubscriptionAsync(_stream, "existing", _settings, DefaultData.AdminCredentials);
		}

		protected override Task When() => Task.CompletedTask;

		[Test]
		public async Task the_completion_succeeds() {
			await _conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings, DefaultData.AdminCredentials);
		}
	}

	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class update_existing_persistent_subscription_with_subscribers<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.End);

		private readonly AutoResetEvent _dropped = new AutoResetEvent(false);
		private SubscriptionDroppedReason _reason;
		private Exception _exception;
		private Exception _caught = null;

		protected override async Task Given() {
			await _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any,
				new EventData(Uuid.NewUuid(), "whatever", Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
			await _conn.CreatePersistentSubscriptionAsync(_stream, "existing", _settings, DefaultData.AdminCredentials)
;
			await _conn.ConnectToPersistentSubscription(_stream, "existing", (x, y) => Task.CompletedTask,
				(sub, reason, ex) => {
					_dropped.Set();
					_reason = reason;
					_exception = ex;
				}, DefaultData.AdminCredentials);
		}

		protected override async Task When() {
			try {
				await _conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings, DefaultData.AdminCredentials);
			} catch (Exception ex) {
				_caught = ex;
			}
		}

		[Test]
		public void the_completion_succeeds() {
			Assert.IsNull(_caught);
		}

		[Test]
		public void existing_subscriptions_are_dropped() {
			Assert.IsTrue(_dropped.WaitOne(TimeSpan.FromSeconds(5)));
			Assert.AreEqual(SubscriptionDroppedReason.ServerError, _reason);
			Assert.IsInstanceOf<PersistentSubscriptionDroppedByServerException>(_exception);
		}
	}


	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class update_non_existing_persistent_subscription<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.End);

		protected override Task When() => Task.CompletedTask;

		[Test]
		public async Task the_completion_fails_with_not_found() {
			await AssertEx.ThrowsAsync<InvalidOperationException>(
				() => _conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings,
					DefaultData.AdminCredentials));
		}
	}

	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class update_existing_persistent_subscription_without_permissions<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings =
			new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.End);

		protected override async Task When() {
			await _conn.AppendToStreamAsync(_stream, ExpectedVersion.Any,
				new EventData(Uuid.NewUuid(), "whatever", Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
			await _conn.CreatePersistentSubscriptionAsync(_stream, "existing", _settings, DefaultData.AdminCredentials)
;
		}

		[Test]
		public async Task the_completion_fails_with_access_denied() {
			await AssertEx.ThrowsAsync<AccessDeniedException>(
				() => _conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings, null));
		}
	}
}
