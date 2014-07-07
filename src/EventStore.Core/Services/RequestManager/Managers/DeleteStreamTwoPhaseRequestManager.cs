using System;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
 
namespace EventStore.Core.Services.RequestManager.Managers
{
    public class DeleteStreamTwoPhaseRequestManager : TwoPhaseRequestManagerBase, 
                                                      IHandle<ClientMessage.DeleteStream>
    {
        private string _eventStreamId;
        private int _expectedVersion;
        private bool _hardDelete;

        public DeleteStreamTwoPhaseRequestManager(IPublisher publisher,  
                                                  int prepareCount, 
                                                  int commitCount, 
                                                  TimeSpan prepareTimeout,
                                                  TimeSpan commitTimeout) 
            : base(publisher, prepareCount, commitCount, prepareTimeout, commitTimeout)
        {
        }

        public void Handle(ClientMessage.DeleteStream request)
        {
            _eventStreamId = request.EventStreamId;
            _expectedVersion = request.ExpectedVersion;
            _hardDelete = request.HardDelete;
            InitNoPreparePhase(request.Envelope, request.InternalCorrId, request.CorrelationId, request.EventStreamId,
                               request.User, StreamAccessType.Delete);
        }

        protected override void OnSecurityAccessGranted(Guid internalCorrId)
        {
            Publisher.Publish(
                new StorageMessage.WriteDelete(
                    internalCorrId, PublishEnvelope, _eventStreamId, _expectedVersion, _hardDelete,
                    liveUntil: NextTimeoutTime - TimeoutOffset));
        }

        protected override void CompleteSuccessRequest(int firstEventNumber, int lastEventNumber, long preparePosition, long commitPosition)
        {
            base.CompleteSuccessRequest(firstEventNumber, lastEventNumber, preparePosition, commitPosition);
            var responseMsg = new ClientMessage.DeleteStreamCompleted(ClientCorrId, OperationResult.Success, null, preparePosition, commitPosition);
            ResponseEnvelope.ReplyWith(responseMsg);
        }

        protected override void CompleteFailedRequest(OperationResult result, string error)
        {
            base.CompleteFailedRequest(result, error);
            var responseMsg = new ClientMessage.DeleteStreamCompleted(ClientCorrId, result, error);
            ResponseEnvelope.ReplyWith(responseMsg);
        }
    }
}