using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;

namespace EventStore.Core.Tests.Services.Replication.RequestManager {
	public class FakeRequestManager : RequestManagerBase {
		public FakeRequestManager(
				IPublisher publisher,
				TimeSpan timeout,
				IEnvelope clientResponseEnvelope,
				Guid interalCorrId,
				Guid clientCorrId,
				string streamId,
				bool betterOrdering,
				long expectedVersion,
				IPrincipal user,
				int prepareCount = 0,
				long transactionId = -1,
				bool waitForCommit = false,
				bool authenticate = true)
			: base(
				 publisher,
				 timeout,
				 clientResponseEnvelope,
				 interalCorrId,
				 clientCorrId,
				 streamId,
				 betterOrdering,
				 expectedVersion,
				 user,
				 prepareCount,
				 transactionId,
				 waitForCommit,
				 authenticate)
				{}
		public override Message WriteRequestMsg => throw new NotImplementedException();

		protected override Message ClientSuccessMsg => throw new NotImplementedException();

		protected override Message ClientFailMsg => throw new NotImplementedException();
	}
}
