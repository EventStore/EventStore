using System;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.RequestManager.Managers
{
    public class WriteStreamTwoPhaseRequestManager : TwoPhaseRequestManagerBase, 
                                                     IHandle<ClientMessage.WriteEvents>
    {
        private ClientMessage.WriteEvents _request;

        public WriteStreamTwoPhaseRequestManager(IPublisher publisher, 
                                                 int prepareCount, 
                                                 int commitCount,
                                                 TimeSpan prepareTimeout,
                                                 TimeSpan commitTimeout)
                : base(publisher, prepareCount, commitCount, prepareTimeout, commitTimeout)
        {
        }

        public void Handle(ClientMessage.WriteEvents request)
        {
            _request = request;
            InitNoPreparePhase(request.Envelope, request.InternalCorrId, request.CorrelationId,
                               request.EventStreamId, request.User, StreamAccessType.Write);
        }

        protected override void OnSecurityAccessGranted(Guid internalCorrId)
        {
            Publisher.Publish(
                new StorageMessage.WritePrepares(
                    internalCorrId, PublishEnvelope, _request.EventStreamId, _request.ExpectedVersion, _request.Events,
                    liveUntil: NextTimeoutTime - TimeoutOffset));
            _request = null;
        }

        protected override void CompleteSuccessRequest(int firstEventNumber, int lastEventNumber, long preparePosition, long commitPosition)
        {
            base.CompleteSuccessRequest(firstEventNumber, lastEventNumber, preparePosition, commitPosition);
            ResponseEnvelope.ReplyWith(new ClientMessage.WriteEventsCompleted(ClientCorrId, firstEventNumber, lastEventNumber, preparePosition, commitPosition));
        }

        protected override void CompleteFailedRequest(OperationResult result, string error)
        {
            base.CompleteFailedRequest(result, error);
            ResponseEnvelope.ReplyWith(new ClientMessage.WriteEventsCompleted(ClientCorrId, result, error));
        }

    }
}