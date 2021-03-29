using System;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class TransactionCommit : RequestManagerBase,
		IHandle<StorageMessage.CommitIndexed> {		
		private bool _transactionWritten;
		public TransactionCommit(
					IPublisher publisher,
					long startOffset,
					TimeSpan timeout,					
					IEnvelope clientResponseEnvelope,
					Guid internalCorrId,
					Guid clientCorrId,
					long transactionId,
					CommitSource commitSource)
			: base(
					 publisher,
					 startOffset,
					 timeout,
					 clientResponseEnvelope,
					 internalCorrId,
					 clientCorrId,
					 expectedVersion: -1,
					 commitSource,
					 transactionId: transactionId,
					 prepareCount: 1,
					 waitForCommit: true) {
			Result = OperationResult.CommitTimeout; // we need an unknown here
		}
		
		protected override Message WriteRequestMsg =>
			new StorageMessage.WriteTransactionEnd(
					InternalCorrId,
					WriteReplyEnvelope,
					TransactionId,
					DateTime.UtcNow + Timeout);

		protected override void AllPreparesWritten() {
			base.AllPreparesWritten();			
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

		public override void Handle(StorageMessage.CommitIndexed message) {
			base.Handle(message);
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
