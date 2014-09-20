using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class CreatePersistentSubscriptionOperation : OperationBase<PersistentSubscriptionCreateResult, ClientMessage.CreatePersistentSubscriptionCompleted>
    {
        private readonly string _stream;
        private readonly string _groupName;
        private readonly bool _resolveLinkTos;
        private readonly int _startFromBeginning;
        private readonly int _messageTimeoutMilliseconds;
        private readonly bool _latencyTracking;
        private readonly int _maxRetryCount;
        private readonly int _liveBufferSize;
        private readonly int _readBatchSize;
        private readonly int _bufferSize;
        private readonly bool _preferRoundRobin;

        public CreatePersistentSubscriptionOperation(ILogger log,
                                       TaskCompletionSource<PersistentSubscriptionCreateResult> source,
                                       string stream,
                                       string groupName,
                                       PersistentSubscriptionSettings settings,
                                       UserCredentials userCredentials)
            : base(log, source, TcpCommand.CreatePersistentSubscription, TcpCommand.CreatePersistentSubscriptionCompleted, userCredentials)
        {
            Ensure.NotNull(settings, "settings");
            _resolveLinkTos = settings.ResolveLinkTos;
            _stream = stream;
            _groupName = groupName;
            _startFromBeginning = settings.StartFrom;
            _maxRetryCount = settings.MaxRetryCount;
            _liveBufferSize = settings.LiveBufferSize;
            _readBatchSize = settings.ReadBatchSize;
            _bufferSize = settings.HistoryBufferSize;
            _latencyTracking = settings.LatencyStatistics;
            _preferRoundRobin = settings.PreferRoundRobin;
            _messageTimeoutMilliseconds = (int) settings.MessageTimeout.TotalMilliseconds;
        }

        protected override object CreateRequestDto()
        {
            return new ClientMessage.CreatePersistentSubscription(_groupName, _stream, _resolveLinkTos, _startFromBeginning, _messageTimeoutMilliseconds,
                                    _latencyTracking, _liveBufferSize, _readBatchSize, _bufferSize, _maxRetryCount, _preferRoundRobin);
        }

        protected override InspectionResult InspectResponse(ClientMessage.CreatePersistentSubscriptionCompleted response)
        {
            switch (response.Result)
            {
                case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Success:
                    Succeed();
                    return new InspectionResult(InspectionDecision.EndOperation, "Success");
                case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Fail:
                    Fail(new InvalidOperationException(String.Format("Subscription group {0} on stream {1} failed '{2}'", _groupName, _stream, response.Reason)));
                    return new InspectionResult(InspectionDecision.EndOperation, "Fail");
                case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AccessDenied:
                    Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.", _stream)));
                    return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
                case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AlreadyExists:
                    Fail(new InvalidOperationException(String.Format("Subscription group {0} on stream {1} alreay exists", _groupName, _stream)));
                    return new InspectionResult(InspectionDecision.EndOperation, "AlreadyExists");
                default:
                    throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
            }
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