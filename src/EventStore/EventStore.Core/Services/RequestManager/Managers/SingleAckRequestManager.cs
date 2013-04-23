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

namespace EventStore.Core.Services.RequestManager.Managers
{
    public class SingleAckRequestManager : IRequestManager,
                                           IHandle<ClientMessage.TransactionStart>,
                                           IHandle<ClientMessage.TransactionWrite>,
                                           IHandle<StorageMessage.PrepareAck>,
                                           IHandle<StorageMessage.WrongExpectedVersion>,
                                           IHandle<StorageMessage.InvalidTransaction>,
                                           IHandle<StorageMessage.StreamDeleted>,
                                           IHandle<StorageMessage.RequestManagerTimerTick>
    {
        private readonly IPublisher _bus;
        private readonly TimeSpan _prepareTimeout;
        private readonly IEnvelope _publishEnvelope;

        private IEnvelope _responseEnvelope;
        private Guid _correlationId;

        private long _transactionId = -1;

        private bool _completed;
        private bool _initialized;
        private DateTime _nextTimeoutTime;

        private RequestType _requestType;

        public SingleAckRequestManager(IPublisher bus, TimeSpan prepareTimeout)
        {
            Ensure.NotNull(bus, "bus");

            _bus = bus;
            _prepareTimeout = prepareTimeout;
            _publishEnvelope = new PublishEnvelope(_bus);
        }

        public void Handle(ClientMessage.TransactionStart request)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;
            _requestType = RequestType.TransactionStart;
            _responseEnvelope = request.Envelope;
            _correlationId = request.CorrelationId;

            _transactionId = -1; // not known yet

            _bus.Publish(new StorageMessage.WriteTransactionStart(_correlationId,
                                                                  _publishEnvelope,
                                                                  request.EventStreamId,
                                                                  request.ExpectedVersion,
                                                                  liveUntil: DateTime.UtcNow + TimeSpan.FromTicks(_prepareTimeout.Ticks * 9 / 10)));
            _nextTimeoutTime = DateTime.UtcNow + _prepareTimeout;
        }

        public void Handle(ClientMessage.TransactionWrite request)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;
            _requestType = RequestType.TransactionWrite;
            _responseEnvelope = request.Envelope;
            _correlationId = request.CorrelationId;

            _transactionId = request.TransactionId;

            _bus.Publish(new StorageMessage.WriteTransactionData(request.CorrelationId, _publishEnvelope, _transactionId, request.Events));
            CompleteSuccessRequest(request.CorrelationId, request.TransactionId);
        }

        public void Handle(StorageMessage.PrepareAck message)
        {
            if (_completed)
                return;
            _transactionId = message.LogPosition;
            CompleteSuccessRequest(_correlationId, _transactionId);
        }

        public void Handle(StorageMessage.WrongExpectedVersion message)
        {
            CompleteFailedRequest(message.CorrelationId, _transactionId, OperationResult.WrongExpectedVersion, "Wrong expected version.");
        }

        public void Handle(StorageMessage.InvalidTransaction message)
        {
            CompleteFailedRequest(message.CorrelationId, _transactionId, OperationResult.InvalidTransaction, "Invalid transaction.");
        }

        public void Handle(StorageMessage.StreamDeleted message)
        {
            CompleteFailedRequest(message.CorrelationId, _transactionId, OperationResult.StreamDeleted, "Stream is deleted.");
        }

        public void Handle(StorageMessage.RequestManagerTimerTick message)
        {
            if (_completed || DateTime.UtcNow < _nextTimeoutTime)
                return;
        
            CompleteFailedRequest(_correlationId, _transactionId, OperationResult.PrepareTimeout, "Prepare phase timeout.");
        }

        private void CompleteSuccessRequest(Guid correlationId, long transactionId)
        {
            _completed = true;
            Message responseMsg;
            switch (_requestType)
            {
                case RequestType.TransactionStart:
                    responseMsg = new ClientMessage.TransactionStartCompleted(correlationId, transactionId, OperationResult.Success, null);
                    break;
                case RequestType.TransactionWrite:
                    responseMsg = new ClientMessage.TransactionWriteCompleted(correlationId, transactionId, OperationResult.Success, null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            _responseEnvelope.ReplyWith(responseMsg);
            _bus.Publish(new StorageMessage.RequestCompleted(correlationId, true));
        }

        private void CompleteFailedRequest(Guid correlationId, long transactionId, OperationResult result, string error)
        {
            Debug.Assert(result != OperationResult.Success);

            _completed = true;
            Message responseMsg;
            switch(_requestType)
            {
                case RequestType.TransactionStart:
                    responseMsg = new ClientMessage.TransactionStartCompleted(correlationId, transactionId, result, error);
                    break;
                case RequestType.TransactionWrite:
                    // Should never happen, only possibly under very heavy load...
                    responseMsg = new ClientMessage.TransactionWriteCompleted(correlationId, transactionId, result, error);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            _responseEnvelope.ReplyWith(responseMsg);
            _bus.Publish(new StorageMessage.RequestCompleted(correlationId, false));
        }

        private enum RequestType
        {
            TransactionStart,
            TransactionWrite
        }
    }
}
