using System;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class TransactionCommit : RequestManagerBase,
		IHandle<StorageMessage.CommitReplicated> {
		private readonly TimeSpan _commitTimeout;
		private readonly bool _betterOrdering;
		private readonly IPrincipal _user;
		private bool _transactionWritten;

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
					ICommitSource commitSource)
			: base(
					 publisher,
					 prepareTimeout,
					 clientResponseEnvelope,
					 interalCorrId,
					 clientCorrId,
					 expectedVersion: -1,
					 commitSource,
					 transactionId: transactionId,
					 prepareCount: 1,
					 waitForCommit: true) {
			_commitTimeout = commitTimeout;
			_betterOrdering = betterOrdering;
			_user = user;
		}

		protected override Message AccessRequestMsg =>
				new StorageMessage.CheckStreamAccess(
						WriteReplyEnvelope,
						InternalCorrId,
						null,
						TransactionId,
						StreamAccessType.Write,
						_user,
						_betterOrdering);


		protected override Message WriteRequestMsg =>
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
		public void Handle(StorageMessage.CommitReplicated message) {
			_transactionWritten = true;
			Committed();
		}
		protected override void Committed() {
			if (!_transactionWritten)
				return;
			base.Committed();
		}
		protected override void ReturnCommitAt(long logPosition, long firstEvent, long lastEvent) {
			_transactionWritten = true;
			base.ReturnCommitAt(logPosition, firstEvent, lastEvent);
		}


	}
}
