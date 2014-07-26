using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class CreatePersistentSubscriptionOperation : OperationBase<PersistentSubscriptionCreateResult, ClientMessage.CreatePersistentSubscriptionCompleted>
    {
        private readonly string _stream;
        private readonly string _groupName;

        public CreatePersistentSubscriptionOperation(ILogger log,
                                       TaskCompletionSource<PersistentSubscriptionCreateResult> source,
                                       string stream,
                                       string groupName,
                                       UserCredentials userCredentials)
            : base(log, source, TcpCommand.CreatePersistentSubscription, TcpCommand.CreatePersistentSubscriptionCompleted, userCredentials)
        {
            _stream = stream;
            _groupName = groupName;
        }

        protected override object CreateRequestDto()
        {
            return new ClientMessage.CreatePersistentSubscription(_stream, _groupName);
        }

        protected override InspectionResult InspectResponse(ClientMessage.CreatePersistentSubscriptionCompleted response)
        {
/*            switch (response.Result)
            {
                case ClientMessage.OperationResult.Success:
                    if (_wasCommitTimeout)
                        Log.Debug("IDEMPOTENT WRITE SUCCEEDED FOR {0}.", this);
                    Succeed();
                    return new InspectionResult(InspectionDecision.EndOperation, "Success");
                case ClientMessage.OperationResult.PrepareTimeout:
                    return new InspectionResult(InspectionDecision.Retry, "PrepareTimeout");
                case ClientMessage.OperationResult.ForwardTimeout:
                    return new InspectionResult(InspectionDecision.Retry, "ForwardTimeout");
                case ClientMessage.OperationResult.CommitTimeout:
                    _wasCommitTimeout = true;
                    return new InspectionResult(InspectionDecision.Retry, "CommitTimeout");
                case ClientMessage.OperationResult.WrongExpectedVersion:
                    var err = string.Format("Append failed due to WrongExpectedVersion. Stream: {0}, Expected version: {1}", _stream, _expectedVersion);
                    Fail(new WrongExpectedVersionException(err));
                    return new InspectionResult(InspectionDecision.EndOperation, "WrongExpectedVersion");
                case ClientMessage.OperationResult.StreamDeleted:
                    Fail(new StreamDeletedException(_stream));
                    return new InspectionResult(InspectionDecision.EndOperation, "StreamDeleted");
                case ClientMessage.OperationResult.InvalidTransaction:
                    Fail(new InvalidTransactionException());
                    return new InspectionResult(InspectionDecision.EndOperation, "InvalidTransaction");
                case ClientMessage.OperationResult.AccessDenied:
                    Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.", _stream)));
                    return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
                default:
                    throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
            }
         */
            return null;
        }

        protected override PersistentSubscriptionCreateResult TransformResponse(ClientMessage.CreatePersistentSubscriptionCompleted response)
        {
            
            return new PersistentSubscriptionCreateResult(PersistentSubscriptionCreateStatus.Success);
        }

        public override string ToString()
        {
            return string.Format("Stream: {0}, Group Name: {1}", _stream, _groupName);
        }
    }
}