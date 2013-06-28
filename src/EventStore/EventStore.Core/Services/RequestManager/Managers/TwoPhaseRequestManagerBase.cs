// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  
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

        protected IEnvelope PublishEnvelope { get { return _publishEnvelope; } }
        protected IEnvelope ResponseEnvelope { get { return _responseEnvelope; } }
        protected Guid ClientCorrId { get { return _clientCorrId; } }
        protected long TransactionPosition { get { return _transactionPos; } }
        protected readonly IPublisher Publisher;
        protected DateTime NextTimeoutTime { get { return _nextTimeoutTime; } }

        private readonly IEnvelope _publishEnvelope;
        
        protected readonly TimeSpan PrepareTimeout;
        protected readonly TimeSpan CommitTimeout;

        private IEnvelope _responseEnvelope;
        private Guid _internalCorrId;
        private Guid _clientCorrId;

        private int _awaitingPrepare;
        private int _awaitingCommit;
        private DateTime _nextTimeoutTime;

        private long _transactionPos = -1;

        private bool _completed;
        private bool _initialized;

        protected TwoPhaseRequestManagerBase(IPublisher publisher, int prepareCount, int commitCount, TimeSpan prepareTimeout, TimeSpan commitTimeout)
        {
            Ensure.NotNull(publisher, "publisher");
            Ensure.Positive(prepareCount, "prepareCount");
            Ensure.Positive(commitCount, "commitCount");

            Publisher = publisher;
            _publishEnvelope = new PublishEnvelope(publisher);

            PrepareTimeout = prepareTimeout;
            CommitTimeout = commitTimeout;

            _awaitingPrepare = prepareCount;
            _awaitingCommit = commitCount;
        }

        protected abstract void OnSecurityAccessGranted(Guid internalCorrId);

        protected void Init(IEnvelope responseEnvelope, Guid internalCorrId, Guid clientCorrId, string eventStreamId,
                            IPrincipal user, long? transactionId, StreamAccessType accessType)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;

            _responseEnvelope = responseEnvelope;
            _internalCorrId = internalCorrId;
            _clientCorrId = clientCorrId;
            _transactionPos = transactionId ?? -1;
        
            _nextTimeoutTime = DateTime.UtcNow + PrepareTimeout;

            Publisher.Publish(new StorageMessage.CheckStreamAccess(
                PublishEnvelope, internalCorrId, eventStreamId, transactionId, accessType, user));
        }

        public void Handle(StorageMessage.CheckStreamAccessCompleted message)
        {
            switch (message.AccessResult)
            {
                case StreamAccessResult.Granted:
                    OnSecurityAccessGranted(_internalCorrId);
                    break;
                case StreamAccessResult.Denied:
                    CompleteFailedRequest(OperationResult.AccessDenied, "Access denied.");
                    break;
                default: throw new Exception(string.Format("Unexpected SecurityAccessResult '{0}'.", message.AccessResult));
            }
        }

        public void Handle(StorageMessage.WrongExpectedVersion message)
        {
            if (_completed)
                return;

            CompleteFailedRequest(OperationResult.WrongExpectedVersion, "Wrong expected version.");
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

        private static readonly ILogger Log = LogManager.GetLoggerFor<TwoPhaseRequestManagerBase>();
        public void Handle(StorageMessage.AlreadyCommitted message)
        {
            Log.Fatal("IDEMPOTENT WRITE TO STREAM ClientCorrelationID {0}, {1}.", _clientCorrId, message);
            CompleteSuccessRequest(message.FirstEventNumber);
        }

        public void Handle(StorageMessage.PrepareAck message)
        {
            if (_completed)
                return;

            if ((message.Flags & PrepareFlags.TransactionBegin) != 0)
                _transactionPos = message.LogPosition;

            if ((message.Flags & PrepareFlags.TransactionEnd) != 0)
            {
                _awaitingPrepare -= 1;
                if (_awaitingPrepare == 0)
                {
                    if (_transactionPos < 0) throw new Exception("PreparePos is not assigned.");
                    Publisher.Publish(new StorageMessage.WriteCommit(message.CorrelationId, _publishEnvelope, _transactionPos));
                    _nextTimeoutTime = DateTime.UtcNow + CommitTimeout;
                }
            }
        }

        public void Handle(StorageMessage.CommitAck message)
        {
            if (_completed)
                return;

            _awaitingCommit -= 1;
            if (_awaitingCommit == 0)
                CompleteSuccessRequest(message.FirstEventNumber);
        }

        protected virtual void CompleteSuccessRequest(int firstEventNumber)
        {
            _completed = true;
            Publisher.Publish(new StorageMessage.RequestCompleted(_internalCorrId, true));
        }

        protected virtual void CompleteFailedRequest(OperationResult result, string error)
        {
            Debug.Assert(result != OperationResult.Success);
            _completed = true;
            Publisher.Publish(new StorageMessage.RequestCompleted(_internalCorrId, false));
        }
    }
}