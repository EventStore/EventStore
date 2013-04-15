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
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.RequestManager.Managers
{
    public abstract class TwoPhaseRequestManagerBase : IRequestManager,
                                                       IHandle<StorageMessage.AlreadyCommitted>,
                                                       IHandle<StorageMessage.PrepareAck>,
                                                       IHandle<StorageMessage.CommitAck>,
                                                       IHandle<StorageMessage.WrongExpectedVersion>,
                                                       IHandle<StorageMessage.StreamDeleted>,
                                                       IHandle<StorageMessage.RequestManagerTimerTick>
    {

        protected IEnvelope PublishEnvelope { get { return _publishEnvelope; } }
        protected IEnvelope ResponseEnvelope { get { return _responseEnvelope; } }
        protected Guid CorrelationId { get { return _correlationId; } }
        protected long TransactionPosition { get { return _transactionPos; } }
        protected readonly IPublisher Publisher;
        private readonly IEnvelope _publishEnvelope;
        
        protected readonly TimeSpan PrepareTimeout;
        protected readonly TimeSpan CommitTimeout;

        private IEnvelope _responseEnvelope;
        private Guid _correlationId;

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

        protected void Init(IEnvelope responseEnvelope, Guid correlationId, long preparePos)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;

            _responseEnvelope = responseEnvelope;
            _correlationId = correlationId;
            _transactionPos = preparePos;
        
            _nextTimeoutTime = DateTime.UtcNow + PrepareTimeout;
        }

        public void Handle(StorageMessage.WrongExpectedVersion message)
        {
            if (_completed)
                return;

            CompleteFailedRequest(message.CorrelationId, OperationResult.WrongExpectedVersion, "Wrong expected version.");
        }

        public void Handle(StorageMessage.StreamDeleted message)
        {
            if (_completed)
                return;

            CompleteFailedRequest(message.CorrelationId, OperationResult.StreamDeleted, "Stream is deleted.");
        }

        public void Handle(StorageMessage.RequestManagerTimerTick message)
        {
            if (_completed || DateTime.UtcNow < _nextTimeoutTime)
                return;

            if (_awaitingPrepare != 0)
                CompleteFailedRequest(_correlationId, OperationResult.PrepareTimeout, "Prepare phase timeout.");
            else 
                CompleteFailedRequest(_correlationId, OperationResult.CommitTimeout, "Commit phase timeout.");
        }

        public void Handle(StorageMessage.AlreadyCommitted message)
        {
            Debug.Assert(message.CorrelationId == _correlationId);
            CompleteSuccessRequest(_correlationId, message.FirstEventNumber);
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
                CompleteSuccessRequest(message.CorrelationId, message.FirstEventNumber);
        }

        protected virtual void CompleteSuccessRequest(Guid correlationId, int firstEventNumber)
        {
            _completed = true;
            Publisher.Publish(new StorageMessage.RequestCompleted(correlationId, true));
        }

        protected virtual void CompleteFailedRequest(Guid correlationId, OperationResult result, string error)
        {
            Debug.Assert(result != OperationResult.Success);
            _completed = true;
            Publisher.Publish(new StorageMessage.RequestCompleted(correlationId, false));
        }
    }
}