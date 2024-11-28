using System;
using System.Security.Claims;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Messages;

public class ITransactionFileTrackerFactoryExtensionsTests {
	readonly ITransactionFileTrackerFactory _factory = new FakeFactory();

	[Fact]
	public void can_get_for_username() {
		var tracker = _factory.For(SystemAccounts.SystemName) as FakeTracker;
		Assert.Equal("system", tracker.Username);
	}

	[Fact]
	public void can_get_for_claims_principal() {
		var tracker = _factory.For(SystemAccounts.System) as FakeTracker;
		Assert.Equal("system", tracker.Username);
	}

	[Fact]
	public void can_get_for_request_message() {
		var tracker = _factory.For(new FakeReadRequest(SystemAccounts.System)) as FakeTracker;
		Assert.Equal("system", tracker.Username);
	}

	[Fact]
	public void can_get_for_null_username() {
		var tracker = _factory.For((string)null) as FakeTracker;
		Assert.Equal("anonymous", tracker.Username);
	}

	[Fact]
	public void can_get_for_null_claims_principal() {
		var tracker = _factory.For((ClaimsPrincipal)null) as FakeTracker;
		Assert.Equal("anonymous", tracker.Username);
	}

	[Fact]
	public void can_get_for_null_request_message() {
		var tracker = _factory.For((FakeReadRequest)null) as FakeTracker;
		Assert.Equal("anonymous", tracker.Username);
	}

	class FakeFactory : ITransactionFileTrackerFactory {
		public ITransactionFileTracker GetOrAdd(string name) => new FakeTracker(name);
	}

	record FakeTracker(string Username) : ITransactionFileTracker {
		public void OnRead(ILogRecord record, ITransactionFileTracker.Source source) {
		}

		public void OnRead(int bytesRead, ITransactionFileTracker.Source source) {
		}
	}

	class FakeReadRequest : ClientMessage.ReadRequestMessage {
		public FakeReadRequest(ClaimsPrincipal user) :
			base(Guid.NewGuid(), Guid.NewGuid(), IEnvelope.NoOp, user, expires: null) {
		}
	}
}
