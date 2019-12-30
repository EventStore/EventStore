using System;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class DeleteStream : RequestManagerBase {
		private readonly bool _hardDelete;

		public DeleteStream(
					IPublisher publisher,
					TimeSpan timeout,
					IEnvelope clientResponseEnvelope,
					Guid interalCorrId,
					Guid clientCorrId,
					string streamId,
					bool betterOrdering,
					long expectedVersion,
					IPrincipal user,
					bool hardDelete)
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
					 prepareCount: 0,
					 waitForCommit: true) {
			_hardDelete = hardDelete;
		}

		public override Message WriteRequestMsg =>
			new StorageMessage.WriteDelete(
					InternalCorrId, 
					WriteReplyEnvelope,
					StreamId,
					ExpectedVersion,
					_hardDelete,
					LiveUntil);

		protected override Message ClientSuccessMsg =>
			 new ClientMessage.DeleteStreamCompleted(
				 ClientCorrId, 
				 OperationResult.Success,
				 null,
				 FirstPrepare,
				 CommitPosition);

		protected override Message ClientFailMsg =>
			new ClientMessage.DeleteStreamCompleted(ClientCorrId, Result, FailureMessage);
	}
}
