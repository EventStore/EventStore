using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class DeletePersistentSubscriptionOperation : OperationBase<PersistentSubscriptionDeleteResult, ClientMessage.DeletePersistentSubscriptionCompleted>
    {
        private readonly string _stream;
        private readonly string _groupName;

        public DeletePersistentSubscriptionOperation(ILogger log,
                                       TaskCompletionSource<PersistentSubscriptionDeleteResult> source,
                                       string stream,
                                       string groupName,
                                       UserCredentials userCredentials)
            : base(log, source, TcpCommand.DeletePersistentSubscription, TcpCommand.DeletePersistentSubscriptionCompleted, userCredentials)
        {
            _stream = stream;
            _groupName = groupName;
        }

        protected override object CreateRequestDto()
        {
            return new ClientMessage.DeletePersistentSubscription(_groupName, _stream);
        }

        protected override InspectionResult InspectResponse(ClientMessage.DeletePersistentSubscriptionCompleted response)
        {
            switch (response.Result)
            {
                case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.Success:
                    Succeed();
                    return new InspectionResult(InspectionDecision.EndOperation, "Success");
                case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.Fail:
                    Fail(new InvalidOperationException(String.Format("Subscription group {0} on stream {1} failed '{2}'", _groupName, _stream, response.Reason)));
                    return new InspectionResult(InspectionDecision.EndOperation, "Fail");
                case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.AccessDenied:
                    Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.", _stream)));
                    return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
                case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.DoesNotExist:
                    Fail(new InvalidOperationException(String.Format("Subscription group {0} on stream {1} does not exist", _groupName, _stream)));
                    return new InspectionResult(InspectionDecision.EndOperation, "AlreadyExists");
                default:
                    throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
            }
        }

        protected override PersistentSubscriptionDeleteResult TransformResponse(ClientMessage.DeletePersistentSubscriptionCompleted response)
        {
            return new PersistentSubscriptionDeleteResult(PersistentSubscriptionDeleteStatus.Success);
        }

        
        public override string ToString()
        {
            return string.Format("Stream: {0}, Group Name: {1}", _stream, _groupName);
        }
    }
}