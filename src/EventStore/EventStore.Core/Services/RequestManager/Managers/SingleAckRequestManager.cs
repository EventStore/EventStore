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
using System.Security.Policy;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;

namespace EventStore.Core.Services.RequestManager.Managers
{
    class SingleAckRequestManager : IHandle<ReplicationMessage.TransactionStartRequestCreated>,
                                           IHandle<ReplicationMessage.TransactionWriteRequestCreated>,
                                           IHandle<ReplicationMessage.PrepareAck>,
                                           IHandle<ReplicationMessage.WrongExpectedVersion>,
                                           IHandle<ReplicationMessage.InvalidTransaction>,
                                           IHandle<ReplicationMessage.StreamDeleted>,
                                           IHandle<ReplicationMessage.PreparePhaseTimeout>
    {
        private readonly IPublisher _bus;
        private readonly IEnvelope _publishEnvelope;

        private IEnvelope _responseEnvelope;
        private Guid _correlationId;
        private string _eventStreamId;

        private long _transactionId = -1;

        private bool _completed;
        private bool _initialized;

        private RequestType _requestType;

        public SingleAckRequestManager(IPublisher bus)
        {
            if (bus == null) throw new ArgumentNullException("bus");

            _bus = bus;
            _publishEnvelope = new PublishEnvelope(_bus);
        }

        public void Handle(ReplicationMessage.TransactionStartRequestCreated message)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;
            _requestType = RequestType.TransactionStart;
            _responseEnvelope = message.Envelope;
            _correlationId = message.CorrelationId;

            _transactionId = -1; // not known yet
            _eventStreamId = message.EventStreamId;

            _bus.Publish(new ReplicationMessage.WriteTransactionStart(_correlationId,
                                                                      _publishEnvelope,
                                                                      message.EventStreamId,
                                                                      message.ExpectedVersion,
                                                                      allowImplicitStreamCreation: true,
                                                                      liveUntil: DateTime.UtcNow + Timeouts.PrepareWriteMessageTimeout));
            _bus.Publish(TimerMessage.Schedule.Create(Timeouts.PrepareTimeout,
                                                      _publishEnvelope,
                                                      new ReplicationMessage.PreparePhaseTimeout(_correlationId)));
        }

        public void Handle(ReplicationMessage.TransactionWriteRequestCreated request)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;
            _requestType = RequestType.TransactionWrite;
            _responseEnvelope = request.Envelope;
            _correlationId = request.CorrelationId;

            _transactionId = request.TransactionId;

            _bus.Publish(new ReplicationMessage.WriteTransactionData(request.CorrelationId,
                                                                     _publishEnvelope,
                                                                     _transactionId,
                                                                     request.EventStreamId,
                                                                     request.Events));
            CompleteSuccessRequest(request.CorrelationId, request.TransactionId, request.EventStreamId);
        }

        public void Handle(ReplicationMessage.PrepareAck message)
        {
            if (_completed)
                return;
            _transactionId = message.LogPosition;
            CompleteSuccessRequest(_correlationId, _transactionId, _eventStreamId);
        }

        public void Handle(ReplicationMessage.WrongExpectedVersion message)
        {
            CompleteFailedRequest(message.CorrelationId, _transactionId, _eventStreamId, OperationErrorCode.WrongExpectedVersion, "Wrong expected version.");
        }

        public void Handle(ReplicationMessage.InvalidTransaction message)
        {
            CompleteFailedRequest(message.CorrelationId, _transactionId, _eventStreamId, OperationErrorCode.WrongExpectedVersion, "Wrong expected version.");
        }

        public void Handle(ReplicationMessage.StreamDeleted message)
        {
            CompleteFailedRequest(message.CorrelationId, _transactionId, _eventStreamId, OperationErrorCode.StreamDeleted, "Stream is deleted.");
        }

        public void Handle(ReplicationMessage.PreparePhaseTimeout message)
        {
            if (_completed)
                return;

            CompleteFailedRequest(message.CorrelationId, _transactionId, _eventStreamId, OperationErrorCode.PrepareTimeout, "Prepare phase timeout.");
        }

        private void CompleteSuccessRequest(Guid correlationId, long transactionId, string eventStreamId)
        {
            _completed = true;
            Message responseMsg;
            switch (_requestType)
            {
                case RequestType.TransactionStart:
                    responseMsg = new ClientMessage.TransactionStartCompleted(correlationId, transactionId, eventStreamId, OperationErrorCode.Success, null);
                    break;
                case RequestType.TransactionWrite:
                    responseMsg = new ClientMessage.TransactionWriteCompleted(correlationId, transactionId, eventStreamId, OperationErrorCode.Success, null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            _responseEnvelope.ReplyWith(responseMsg);
            _bus.Publish(new ReplicationMessage.RequestCompleted(correlationId, true));
        }

        private void CompleteFailedRequest(Guid correlationId, long transactionId, string eventStreamId, OperationErrorCode errorCode, string error)
        {
            Debug.Assert(errorCode != OperationErrorCode.Success);

            _completed = true;
            Message responseMsg;
            switch(_requestType)
            {
                case RequestType.TransactionStart:
                    responseMsg = new ClientMessage.TransactionStartCompleted(correlationId, transactionId, eventStreamId, errorCode, error);
                    break;
                case RequestType.TransactionWrite:
                    // Should never happen, only possibly under very heavy load...
                    responseMsg = new ClientMessage.TransactionWriteCompleted(correlationId, transactionId, eventStreamId, errorCode, error);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            _responseEnvelope.ReplyWith(responseMsg);
            _bus.Publish(new ReplicationMessage.RequestCompleted(correlationId, false));
        }

        private enum RequestType
        {
            TransactionStart,
            TransactionWrite
        }
    }
}
