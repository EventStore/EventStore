using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;

namespace EventStore.Core.Tests.Services.Replication.RequestManager {
	public class FakeRequestManager : RequestManagerBase {
		public FakeRequestManager(
				IPublisher publisher,
				TimeSpan timeout,
				IEnvelope clientResponseEnvelope,
				Guid interalCorrId,
				Guid clientCorrId,
				long expectedVersion,
				ICommitSource commitSource,
				int prepareCount = 0,
				long transactionId = -1,
				bool waitForCommit = false)
			: base(
				 publisher,
				 timeout,
				 clientResponseEnvelope,
				 interalCorrId,
				 clientCorrId,
				 expectedVersion,
				 commitSource,
				 prepareCount,
				 transactionId,
				 waitForCommit)
				{}
		protected override Message AccessRequestMsg => throw new NotImplementedException();
		protected override Message WriteRequestMsg => throw new NotImplementedException();
		protected override Message ClientSuccessMsg => throw new NotImplementedException();
		protected override Message ClientFailMsg => throw new NotImplementedException();
	}
}
