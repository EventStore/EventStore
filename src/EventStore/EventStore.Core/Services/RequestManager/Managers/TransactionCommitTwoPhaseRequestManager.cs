using System;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.RequestManager.Managers
{
    public class TransactionCommitTwoPhaseRequestManager : TwoPhaseRequestManagerBase, 
                                                           IHandle<ClientMessage.TransactionCommit>
    {
        private long _transactionId;

        public TransactionCommitTwoPhaseRequestManager(IPublisher publisher, 
                                                       int prepareCount, 
                                                       int commitCount, 
                                                       TimeSpan prepareTimeout, 
                                                       TimeSpan commitTimeout)
            : base(publisher, prepareCount, commitCount, prepareTimeout, commitTimeout)
        {
        }

        public void Handle(ClientMessage.TransactionCommit request)
        {
            _transactionId = request.TransactionId;
            InitTwoPhase(request.Envelope, request.InternalCorrId, request.CorrelationId,
                         request.TransactionId, request.User, StreamAccessType.Write);
        }

        protected override void OnSecurityAccessGranted(Guid internalCorrId)
        {
            Publisher.Publish(
                new StorageMessage.WriteTransactionPrepare(
                    internalCorrId, PublishEnvelope, _transactionId, liveUntil: NextTimeoutTime - TimeoutOffset));
        }

        protected override void CompleteSuccessRequest(int firstEventNumber, int lastEventNumber, long preparePosition, long commitPosition)
        {
            base.CompleteSuccessRequest(firstEventNumber, lastEventNumber);
            var responseMsg = new ClientMessage.TransactionCommitCompleted(ClientCorrId, _transactionId, firstEventNumber, lastEventNumber);
            ResponseEnvelope.ReplyWith(responseMsg);
        }

        protected override void CompleteFailedRequest(OperationResult result, string error)
        {
            base.CompleteFailedRequest(result, error);
            var responseMsg = new ClientMessage.TransactionCommitCompleted(ClientCorrId, _transactionId, result, error);
            ResponseEnvelope.ReplyWith(responseMsg);
        }

    }
}