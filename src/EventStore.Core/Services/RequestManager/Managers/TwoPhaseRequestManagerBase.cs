using System;
using System.Diagnostics;
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.RequestManager.Managers
{
    public abstract class TwoPhaseRequestManagerBase : IRequestManager,
                                                       IHandle<StorageMessage.CheckStreamAccessCompleted>,
                                                       IHandle<StorageMessage.AlreadyCommitted>,
                                                       IHandle<StorageMessage.PrepareAck>,
                                                       IHandle<StorageMessage.CommitAck>,
                                                       IHandle<StorageMessage.WrongExpectedVersion>,
                                                       IHandle<StorageMessage.StreamDeleted>,
                                                       IHandle<StorageMessage.RequestManagerTimerTick>
    {
        internal static readonly TimeSpan TimeoutOffset = TimeSpan.FromMilliseconds(30);
        private static readonly ILogger Log = LogManager.GetLoggerFor<TwoPhaseRequestManagerBase>();

        protected readonly IPublisher Publisher;
        protected readonly IEnvelope PublishEnvelope;
        protected IEnvelope ResponseEnvelope { get { return _responseEnvelope; } }
        protected Guid ClientCorrId { get { return _clientCorrId; } }
        protected DateTime NextTimeoutTime { get { return _nextTimeoutTime; } }

        protected readonly TimeSpan PrepareTimeout;
        protected readonly TimeSpan CommitTimeout;
        private bool _hadSelf = false;
        private IEnvelope _responseEnvelope;
        private Guid _internalCorrId;
        private Guid _clientCorrId;
        private long _transactionId = -1;

        private int _awaitingPrepare;
        private int _awaitingCommit;
        private DateTime _nextTimeoutTime;

        private bool _completed;
        private bool _initialized;
        private bool _betterOrdering;
        protected TwoPhaseRequestManagerBase(IPublisher publisher,
                                                                       int prepareCount,
                                                                       int commitCount,
                                                                       TimeSpan prepareTimeout,
                                                                       TimeSpan commitTimeout,
                                                                       bool betterOrdering)
        {
            Ensure.NotNull(publisher, "publisher");
            Ensure.Positive(prepareCount, "prepareCount");
            Ensure.Positive(commitCount, "commitCount");

            Publisher = publisher;
            PublishEnvelope = new PublishEnvelope(publisher);

            PrepareTimeout = prepareTimeout;
            CommitTimeout = commitTimeout;
            _betterOrdering = betterOrdering;

            _awaitingPrepare = prepareCount;
            _awaitingCommit = commitCount;
        }

        protected abstract void OnSecurityAccessGranted(Guid internalCorrId);

        protected void InitNoPreparePhase(IEnvelope responseEnvelope, Guid internalCorrId, Guid clientCorrId,
                                          string eventStreamId, IPrincipal user, StreamAccessType accessType)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;

            _responseEnvelope = responseEnvelope;
            _internalCorrId = internalCorrId;
            _clientCorrId = clientCorrId;

            _nextTimeoutTime = DateTime.UtcNow + CommitTimeout;
            _awaitingPrepare = 0;
            Publisher.Publish(new StorageMessage.CheckStreamAccess(
                PublishEnvelope, internalCorrId, eventStreamId, null, accessType, user, _betterOrdering));
        }

        protected void InitTwoPhase(IEnvelope responseEnvelope, Guid internalCorrId, Guid clientCorrId,
                                    long transactionId, IPrincipal user, StreamAccessType accessType)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;

            _responseEnvelope = responseEnvelope;
            _internalCorrId = internalCorrId;
            _clientCorrId = clientCorrId;
            _transactionId = transactionId;

            _nextTimeoutTime = DateTime.UtcNow + PrepareTimeout;

            Publisher.Publish(new StorageMessage.CheckStreamAccess(
                PublishEnvelope, internalCorrId, null, transactionId, accessType, user));
        }

        public void Handle(StorageMessage.CheckStreamAccessCompleted message)
        {
            if (message.AccessResult.Granted)
                OnSecurityAccessGranted(_internalCorrId);
            else
                CompleteFailedRequest(OperationResult.AccessDenied, "Access denied.");
        }

        public void Handle(StorageMessage.WrongExpectedVersion message)
        {
            if (_completed)
                return;

            CompleteFailedRequest(OperationResult.WrongExpectedVersion, "Wrong expected version.", message.CurrentVersion);
        }

        public void Handle(StorageMessage.StreamDeleted message)
        {
            if (_completed)
                return;

            CompleteFailedRequest(OperationResult.StreamDeleted, "Stream is deleted.");
        }

        public void Handle(StorageMessage.RequestManagerTimerTick message)
        {
            if (_completed || message.UtcNow < _nextTimeoutTime)
                return;

            if (_awaitingPrepare != 0)
                CompleteFailedRequest(OperationResult.PrepareTimeout, "Prepare phase timeout.");
            else
                CompleteFailedRequest(OperationResult.CommitTimeout, "Commit phase timeout.");
        }

        public void Handle(StorageMessage.AlreadyCommitted message)
        {
            Log.Trace("IDEMPOTENT WRITE TO STREAM ClientCorrelationID {0}, {1}.", _clientCorrId, message);
            CompleteSuccessRequest(message.FirstEventNumber, message.LastEventNumber, -1, -1);
            //TODO GFY WE NEED TO GET THE LOG POSITION HERE WHEN ITS AN IDEMPOTENT WRITE
        }

        public void Handle(StorageMessage.PrepareAck message)
        {
            if (_completed)
                return;

            if (_transactionId == -1)
                throw new Exception("TransactionId was not set, transactionId = -1.");

            if (message.Flags.HasAnyOf(PrepareFlags.TransactionEnd))
            {
                _awaitingPrepare -= 1;
                if (_awaitingPrepare == 0)
                {
                    Publisher.Publish(new StorageMessage.WriteCommit(message.CorrelationId, PublishEnvelope, _transactionId));
                    _nextTimeoutTime = DateTime.UtcNow + CommitTimeout;
                }
            }
        }

        public void Handle(StorageMessage.CommitAck message)
        {
            if (_completed)
                return;

            _awaitingCommit -= 1;
            if(message.IsSelf) _hadSelf = true;
            if (_awaitingCommit == 0 && _hadSelf)
                CompleteSuccessRequest(message.FirstEventNumber, message.LastEventNumber, message.LogPosition, message.LogPosition);
        }

        protected virtual void CompleteSuccessRequest(int firstEventNumber, int lastEventNumber, long preparePosition, long commitPosition)
        {
            _completed = true;
            Publisher.Publish(new StorageMessage.RequestCompleted(_internalCorrId, true));
        }

        protected virtual void CompleteFailedRequest(OperationResult result, string error, int currentVersion = -1)
        {
            Debug.Assert(result != OperationResult.Success);
            _completed = true;
            Publisher.Publish(new StorageMessage.RequestCompleted(_internalCorrId, false, currentVersion));
        }
    }
}