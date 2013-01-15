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
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;

namespace EventStore.Core.Services.RequestManager.Managers
{
    public class TransactionCommitTwoPhaseRequestManager : TwoPhaseRequestManagerBase, IHandle<StorageMessage.TransactionCommitRequestCreated>
    {
        public TransactionCommitTwoPhaseRequestManager(IPublisher publisher, int prepareCount, int commitCount) :
                base(publisher, prepareCount, commitCount)
        {
        }

        public void Handle(StorageMessage.TransactionCommitRequestCreated request)
        {
            Init(request.Envelope, request.CorrelationId, request.TransactionId);

            Publisher.Publish(new StorageMessage.WriteTransactionPrepare(request.CorrelationId,
                                                                         PublishEnvelope,
                                                                         request.TransactionId,
                                                                         liveUntil: DateTime.UtcNow + Timeouts.PrepareWriteMessageTimeout));
            Publisher.Publish(TimerMessage.Schedule.Create(Timeouts.PrepareTimeout,
                                                           PublishEnvelope,
                                                           new StorageMessage.PreparePhaseTimeout(CorrelationId)));
        }

        protected override void CompleteSuccessRequest(Guid correlationId, int firstEventNumber)
        {
            base.CompleteSuccessRequest(correlationId, firstEventNumber);
            var responseMsg = new ClientMessage.TransactionCommitCompleted(correlationId, TransactionPosition, OperationResult.Success, null);
            ResponseEnvelope.ReplyWith(responseMsg);
        }

        protected override void CompleteFailedRequest(Guid correlationId, OperationResult result, string error)
        {
            base.CompleteFailedRequest(correlationId, result, error);
            var responseMsg = new ClientMessage.TransactionCommitCompleted(correlationId, TransactionPosition, result, error);
            ResponseEnvelope.ReplyWith(responseMsg);
        }

    }
}