using System;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class TransactionCommitTwoPhaseRequestManager : TwoPhaseRequestManagerBase,
		IHandle<ClientMessage.TransactionCommit> {
		private long _transactionId;
		private ClientMessage.TransactionCommitCompleted _responseMsg;

		public TransactionCommitTwoPhaseRequestManager(IPublisher publisher,
			int prepareCount,
			TimeSpan prepareTimeout,
			TimeSpan commitTimeout,
			bool betterOrdering)
			: base(publisher, prepareCount, prepareTimeout, commitTimeout, betterOrdering) {
		}

		public void Handle(ClientMessage.TransactionCommit request) {
			_transactionId = request.TransactionId;
			InitTwoPhase(request.Envelope, request.InternalCorrId, request.CorrelationId,
				request.TransactionId, request.User, StreamAccessType.Write);
		}

		protected override void OnSecurityAccessGranted(Guid internalCorrId) {
			Publisher.Publish(
				new StorageMessage.WriteTransactionPrepare(
					internalCorrId, PublishEnvelope, _transactionId, liveUntil: NextTimeoutTime - TimeoutOffset));
		}

		
		protected override void SuccessLocalCommitted(long firstEventNumber, long lastEventNumber,
			long preparePosition, long commitPosition) {
			base.SuccessLocalCommitted(firstEventNumber, lastEventNumber, preparePosition, commitPosition);
			_responseMsg =  new ClientMessage.TransactionCommitCompleted(ClientCorrId, _transactionId,
				firstEventNumber, lastEventNumber, preparePosition, commitPosition);

		}

		protected override void SuccessClusterCommitted() {
			base.SuccessClusterCommitted();
			ResponseEnvelope.ReplyWith(_responseMsg);
		}
		protected override void CompleteFailedRequest(OperationResult result, string error, long currentVersion) {
			base.CompleteFailedRequest(result, error, currentVersion);
			var responseMsg = new ClientMessage.TransactionCommitCompleted(ClientCorrId, _transactionId, result, error);
			ResponseEnvelope.ReplyWith(responseMsg);
		}
	}
}
