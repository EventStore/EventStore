using System;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;

namespace EventStore.Core.Tests.Services.RequestManagement {
	public class FakeRequestManager : RequestManagerBase {
		public FakeRequestManager(
				IPublisher publisher,
				long startTime,
				TimeSpan phaseTimeout,
				IEnvelope clientResponseEnvelope,
				Guid internalCorrId,
				Guid clientCorrId,
				long expectedVersion,
				CommitSource commitSource,
				int prepareCount = 0,
				long transactionId = -1,
				bool waitForCommit = false)
			: base(
				 publisher,
				 startTime,
				 phaseTimeout,
				 clientResponseEnvelope,
				 internalCorrId,
				 clientCorrId,
				 expectedVersion,
				 commitSource,
				 prepareCount,
				 transactionId,
				 waitForCommit)
				{}
		protected override Message WriteRequestMsg => throw new NotImplementedException();
		protected override Message ClientSuccessMsg => throw new NotImplementedException();
		protected override Message ClientFailMsg => throw new NotImplementedException();

		public override void Handle(StorageMessage.PrepareAck message) {
			throw new NotImplementedException();
		}

		public override void Handle(StorageMessage.CommitIndexed message) {
			throw new NotImplementedException();
		}

		protected override Task WaitForClusterCommit() {
			throw new NotImplementedException();
		}

		protected override Task WaitForLocalCommit() {
			throw new NotImplementedException();
		}

		protected override Task WaitForLocalIndex() {
			throw new NotImplementedException();
		}
	}
}
