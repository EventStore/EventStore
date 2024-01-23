using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Tcp;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Authentication;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using System.Text;
using EventStore.Client.Messages;
using EventStore.Core.Authentication.InternalAuthentication;
using EventStore.Core.LogV2;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Services;
using EventStore.Core.Tests.Authorization;
using EventStore.Core.Util;
using EventRecord = EventStore.Core.Data.EventRecord;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Core.Tests.Services.Transport.Tcp {
	[TestFixture]
	public class TcpClientDispatcherTests {
		private readonly NoopEnvelope _envelope = new NoopEnvelope();

		private ClientTcpDispatcher _dispatcher;
		private TcpConnectionManager _connection;

		[OneTimeSetUp]
		public void Setup() {
			_dispatcher = new ClientTcpDispatcher(2000);
			
			var dummyConnection = new DummyTcpConnection();
			_connection = new TcpConnectionManager(
				Guid.NewGuid().ToString(), TcpServiceType.External, new ClientTcpDispatcher(2000),
				InMemoryBus.CreateTest(), dummyConnection, InMemoryBus.CreateTest(), new InternalAuthenticationProvider(
					InMemoryBus.CreateTest(), new Core.Helpers.IODispatcher(InMemoryBus.CreateTest(), new NoopEnvelope()),
					new StubPasswordHashAlgorithm(), 1, false, DefaultData.DefaultUserOptions),
				new AuthorizationGateway(new TestAuthorizationProvider()), 
				TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), (man, err) => { },
				Opts.ConnectionPendingSendBytesThresholdDefault, Opts.ConnectionQueueSizeThresholdDefault);
		}

		[Test]
		public void
			when_wrapping_stream_event_appeared_with_deleted_event_should_not_downgrade_version_for_v2_clients() {
			var msg = new ClientMessage.StreamEventAppeared(Guid.NewGuid(),
				ResolvedEvent.ForUnresolvedEvent(CreateDeletedEventRecord(), 0));

			var package = _dispatcher.WrapMessage(msg, (byte)ClientVersion.V2);
			Assert.IsNotNull(package, "Package is null");
			Assert.AreEqual(TcpCommand.StreamEventAppeared, package.Value.Command, "TcpCommand");

			var dto = package.Value.Data.Deserialize<StreamEventAppeared>();
			Assert.IsNotNull(dto, "DTO is null");
			Assert.AreEqual(long.MaxValue, dto.Event.Event.EventNumber, "Event Number");
		}

		[Test]
		public void
			when_wrapping_subscribe_to_stream_confirmation_when_stream_deleted_should_not_downgrade_version_for_v2_clients() {
			var msg = new ClientMessage.SubscriptionConfirmation(Guid.NewGuid(), 100, long.MaxValue);
			var package = _dispatcher.WrapMessage(msg, (byte)ClientVersion.V2);
			Assert.IsNotNull(package, "Package is null");
			Assert.AreEqual(TcpCommand.SubscriptionConfirmation, package.Value.Command, "TcpCommand");

			var dto = package.Value.Data.Deserialize<SubscriptionConfirmation>();
			Assert.IsNotNull(dto, "DTO is null");
			Assert.AreEqual(long.MaxValue, dto.LastEventNumber, "Last Event Number");
		}

		[Test]
		public void
			when_wrapping_subscribe_to_stream_confirmation_when_stream_deleted_should_not_downgrade_last_event_number_for_v2_clients() {
			var msg = new ClientMessage.SubscriptionConfirmation(Guid.NewGuid(), 100, long.MaxValue);
			var package = _dispatcher.WrapMessage(msg, (byte)ClientVersion.V2);
			Assert.IsNotNull(package, "Package is null");
			Assert.AreEqual(TcpCommand.SubscriptionConfirmation, package.Value.Command, "TcpCommand");

			var dto = package.Value.Data.Deserialize<SubscriptionConfirmation>();
			Assert.IsNotNull(dto, "DTO is null");
			Assert.AreEqual(long.MaxValue, dto.LastEventNumber, "Last Event Number");
		}

		[Test]
		public void
			when_wrapping_stream_event_appeared_with_link_to_deleted_event_should_not_downgrade_version_for_v2_clients() {
			var msg = new ClientMessage.StreamEventAppeared(Guid.NewGuid(),
				ResolvedEvent.ForResolvedLink(CreateLinkEventRecord(), CreateDeletedEventRecord(), 0));

			var package = _dispatcher.WrapMessage(msg, (byte)ClientVersion.V2);
			Assert.IsNotNull(package, "Package is null");
			Assert.AreEqual(TcpCommand.StreamEventAppeared, package.Value.Command, "TcpCommand");

			var dto = package.Value.Data.Deserialize<StreamEventAppeared>();
			Assert.IsNotNull(dto, "DTO is null");
			Assert.AreEqual(0, dto.Event.Event.EventNumber, "Event Number");
			Assert.AreEqual(long.MaxValue, dto.Event.Link.EventNumber, "Link Event Number");
		}

		[Test]
		public void 
			when_wrapping_scavenge_started_response_should_return_result_and_scavengeId_for_v2_clients() {
			var scavengeId = Guid.NewGuid().ToString();
			var msg = new ClientMessage.ScavengeDatabaseStartedResponse(Guid.NewGuid(), scavengeId);

			var package = _dispatcher.WrapMessage(msg, (byte)ClientVersion.V2);
			Assert.IsNotNull(package, "Package is null");
			Assert.AreEqual(TcpCommand.ScavengeDatabaseResponse, package.Value.Command, "TcpCommand");

			var dto = package.Value.Data.Deserialize<ScavengeDatabaseResponse>();
			Assert.IsNotNull(dto, "DTO is null");
			Assert.AreEqual(dto.Result, ScavengeDatabaseResponse.Types.ScavengeResult.Started);
			Assert.AreEqual(dto.ScavengeId, scavengeId);
		}
		
		[Test]
		public void 
			when_wrapping_scavenge_inprogress_response_should_return_result_and_scavengeId_for_v2_clients() {
			var scavengeId = Guid.NewGuid().ToString();
			var msg = new ClientMessage.ScavengeDatabaseInProgressResponse(Guid.NewGuid(), scavengeId, reason:"In Progress");

			var package = _dispatcher.WrapMessage(msg, (byte)ClientVersion.V2);
			Assert.IsNotNull(package, "Package is null");
			Assert.AreEqual(TcpCommand.ScavengeDatabaseResponse, package.Value.Command, "TcpCommand");
			
			var dto = package.Value.Data.Deserialize<ScavengeDatabaseResponse>();
			Assert.IsNotNull(dto, "DTO is null");
			Assert.AreEqual(dto.Result, ScavengeDatabaseResponse.Types.ScavengeResult.InProgress);
			Assert.AreEqual(dto.ScavengeId, scavengeId);
		}

		[Test]
		public void 
			when_wrapping_scavenge_unauthorized_response_should_return_result_and_scavengeId_for_v2_clients() {
			var scavengeId = Guid.NewGuid().ToString();
			var msg = new ClientMessage.ScavengeDatabaseUnauthorizedResponse(Guid.NewGuid(), scavengeId,"Unauthorized" );

			var package = _dispatcher.WrapMessage(msg, (byte)ClientVersion.V2);
			Assert.IsNotNull(package, "Package is null");
			Assert.AreEqual(TcpCommand.ScavengeDatabaseResponse, package.Value.Command, "TcpCommand");

			var dto = package.Value.Data.Deserialize<ScavengeDatabaseResponse>();
			Assert.IsNotNull(dto, "DTO is null");
			Assert.AreEqual(dto.Result, ScavengeDatabaseResponse.Types.ScavengeResult.Unauthorized);
			Assert.AreEqual(dto.ScavengeId, scavengeId);
		}

		private EventRecord CreateDeletedEventRecord() {
			return new EventRecord(long.MaxValue,
				LogRecord.DeleteTombstone(new LogV2RecordFactory(), 0, Guid.NewGuid(), Guid.NewGuid(),
					"test-stream", "test-type", long.MaxValue), "test-stream", SystemEventTypes.StreamDeleted);
		}

		private EventRecord CreateLinkEventRecord() {
			return new EventRecord(0, LogRecord.Prepare(new LogV2RecordFactory(), 100, Guid.NewGuid(), Guid.NewGuid(), 0, 0,
				"link-stream", -1, PrepareFlags.SingleWrite | PrepareFlags.Data, SystemEventTypes.LinkTo,
				Encoding.UTF8.GetBytes(string.Format("{0}@test-stream", long.MaxValue)), new byte[0]), "link-stream", SystemEventTypes.LinkTo);
		}
	}
}
