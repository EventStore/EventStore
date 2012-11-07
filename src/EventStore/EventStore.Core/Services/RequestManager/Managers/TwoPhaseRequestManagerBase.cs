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
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.RequestManager.Managers
{
    public class TwoPhaseRequestManagerBase :           IHandle<StorageMessage.AlreadyCommitted>,
                                                        IHandle<StorageMessage.PrepareAck>,
                                                        IHandle<StorageMessage.CommitAck>,
                                                        IHandle<StorageMessage.WrongExpectedVersion>,
                                                        IHandle<StorageMessage.StreamDeleted>,
                                                        IHandle<StorageMessage.PreparePhaseTimeout>,
                                                        IHandle<StorageMessage.CommitPhaseTimeout>
    {
         
        protected readonly IPublisher Publisher;
        protected readonly IEnvelope _publishEnvelope;
        protected IEnvelope _responseEnvelope;
        protected Guid _correlationId;
        protected string _eventStreamId;

        protected int _awaitingPrepare;
        protected int _awaitingCommit;

        protected long _preparePos = -1;

        protected bool _completed;
        protected bool _initialized;

        public TwoPhaseRequestManagerBase(IPublisher publisher, int prepareCount, int commitCount)
        {
            if (publisher == null) 
                throw new ArgumentNullException();
            if (prepareCount <= 0 || commitCount <= 0) 
                throw new ArgumentOutOfRangeException("counts for prepare and commit acks must be a positive number");
            Publisher = publisher;
            _awaitingCommit = commitCount;
            _awaitingPrepare = prepareCount;
            _publishEnvelope = new PublishEnvelope(publisher);
        }


        public void Handle(StorageMessage.WrongExpectedVersion message)
        {
            if (_completed)
                return;

            CompleteFailedRequest(message.CorrelationId, _eventStreamId, OperationErrorCode.WrongExpectedVersion, "Wrong expected version.");
        }

        public void Handle(StorageMessage.StreamDeleted message)
        {
            if (_completed)
                return;

            CompleteFailedRequest(message.CorrelationId, _eventStreamId, OperationErrorCode.StreamDeleted, "Stream is deleted.");
        }

        public void Handle(StorageMessage.PreparePhaseTimeout message)
        {
            if (_completed || _awaitingPrepare == 0)
                return;

            CompleteFailedRequest(message.CorrelationId, _eventStreamId, OperationErrorCode.PrepareTimeout, "Prepare phase timeout.");
        }

        public void Handle(StorageMessage.CommitPhaseTimeout message)
        {
            if (_completed || _awaitingCommit == 0 || _awaitingPrepare != 0)
                return;

            CompleteFailedRequest(message.CorrelationId, _eventStreamId, OperationErrorCode.CommitTimeout, "Commit phase timeout.");
        }


        public void Handle(StorageMessage.AlreadyCommitted message)
        {
            Debug.Assert(message.EventStreamId == _eventStreamId && message.CorrelationId == _correlationId);
            CompleteSuccessRequest(_correlationId, _eventStreamId, message.StartEventNumber);
        }

        public void Handle(StorageMessage.PrepareAck message)
        {
            if (_completed)
                return;

            if ((message.Flags & PrepareFlags.TransactionBegin) != 0)
                _preparePos = message.LogPosition;

            if ((message.Flags & PrepareFlags.TransactionEnd) != 0)
            {
                _awaitingPrepare -= 1;
                if (_awaitingPrepare == 0)
                {
                    Publisher.Publish(new StorageMessage.WriteCommit(message.CorrelationId, _publishEnvelope, _preparePos));
                    Publisher.Publish(TimerMessage.Schedule.Create(Timeouts.CommitTimeout,
                                                                   _publishEnvelope,
                                                                   new StorageMessage.CommitPhaseTimeout(_correlationId)));
                }
            }
        }

        public void Handle(StorageMessage.CommitAck message)
        {
            if (_completed)
                return;

            _awaitingCommit -= 1;
            if (_awaitingCommit == 0)
                CompleteSuccessRequest(message.CorrelationId, _eventStreamId, message.EventNumber);
        }

        protected virtual void CompleteSuccessRequest(Guid correlationId, string eventStreamId, int startEventNumber)
        {
            _completed = true;
            Publisher.Publish(new StorageMessage.RequestCompleted(correlationId, true));
        }

        protected virtual void CompleteFailedRequest(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error)
        {
            Debug.Assert(errorCode != OperationErrorCode.Success);
            _completed = true;
            Publisher.Publish(new StorageMessage.RequestCompleted(correlationId, false));
        }
    }
}