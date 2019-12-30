using System;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class TransactionCommit : RequestManagerBase {
		private readonly TimeSpan _commitTimeout;

		public TransactionCommit(
					IPublisher publisher,
					TimeSpan prepareTimeout,
					TimeSpan commitTimeout,
					IEnvelope clientResponseEnvelope,
					Guid interalCorrId,
					Guid clientCorrId,
					long transactionId,
					bool betterOrdering,
					IPrincipal user,
					long currentCommittedPosition = 0)
			: base(
					 publisher,
					 prepareTimeout,
					 clientResponseEnvelope,
					 interalCorrId,
					 clientCorrId,
					 streamId: nameof(TransactionCommit),
					 betterOrdering: betterOrdering,
					 expectedVersion: -1,
					 user: user,
					 transactionId: transactionId,
					 prepareCount: 1,
					 waitForCommit: true,
					 currentLogPosition: currentCommittedPosition) {
			_commitTimeout = commitTimeout + TimeSpan.FromSeconds(2);
		}

		public override Message WriteRequestMsg =>
			new StorageMessage.WriteTransactionEnd(
					InternalCorrId,
					WriteReplyEnvelope,
					TransactionId,
					LiveUntil);

		protected override void AllPreparesWritten() {
			base.AllPreparesWritten();
			NextTimeoutTime = DateTime.UtcNow + _commitTimeout;
			Publisher.Publish(
				new StorageMessage.WriteCommit(
						InternalCorrId,
						WriteReplyEnvelope,
						TransactionId));
		}

		protected override Message ClientSuccessMsg =>
			 new ClientMessage.TransactionCommitCompleted(
					ClientCorrId,
					TransactionId,
					FirstEventNumber,
					LastEventNumber,
					CommitPosition,   //not technically correct, but matches current behavior correctly
					CommitPosition);

		protected override Message ClientFailMsg =>
			 new ClientMessage.TransactionCommitCompleted(
					ClientCorrId,
					TransactionId,
					Result,
					FailureMessage);

	}
}
