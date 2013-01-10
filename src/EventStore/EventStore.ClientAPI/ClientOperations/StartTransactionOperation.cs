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
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class StartTransactionOperation : IClientOperation
    {
        private readonly TaskCompletionSource<EventStoreTransaction> _source;
        private ClientMessage.TransactionStartCompleted _result;
        private int _completed;

        private Guid _corrId;
        private readonly object _corrIdLock = new object();

        private readonly bool _forward;
        private readonly string _stream;
        private readonly int _expectedVersion;
        private readonly EventStoreConnection _parentConnection;

        public Guid CorrelationId
        {
            get
            {
                lock (_corrIdLock)
                    return _corrId;
            }
        }

        public StartTransactionOperation(TaskCompletionSource<EventStoreTransaction> source,
                                         Guid corrId,
                                         bool forward,
                                         string stream,
                                         int expectedVersion,
                                         EventStoreConnection parentConnection)
        {
            _source = source;
            _corrId = corrId;
            _forward = forward;
            _stream = stream;
            _expectedVersion = expectedVersion;
            _parentConnection = parentConnection;
        }

        public void SetRetryId(Guid correlationId)
        {
            lock (_corrIdLock)
                _corrId = correlationId;
        }

        public TcpPackage CreateNetworkPackage()
        {
            lock (_corrIdLock)
            {
                var startTransaction = new ClientMessage.TransactionStart(_stream, _expectedVersion, _forward);
                return new TcpPackage(TcpCommand.TransactionStart, _corrId,  startTransaction.Serialize());
            }
        }

        public InspectionResult InspectPackage(TcpPackage package)
        {
            try
            {
                if (package.Command == TcpCommand.DeniedToRoute)
                {
                    var route = package.Data.Deserialize<ClientMessage.DeniedToRoute>();
                    return new InspectionResult(InspectionDecision.Reconnect, data: route.ExternalTcpEndPoint);
                }
                if (package.Command != TcpCommand.TransactionStartCompleted)
                {
                    return new InspectionResult(InspectionDecision.NotifyError,
                                                new CommandNotExpectedException(TcpCommand.TransactionStartCompleted.ToString(),
                                                                                package.Command.ToString()));
                }

                var data = package.Data;
                var dto = data.Deserialize<ClientMessage.TransactionStartCompleted>();
                _result = dto;

                switch (dto.Result)
                {
                    case ClientMessage.OperationResult.Success:
                        return new InspectionResult(InspectionDecision.Succeed);
                    case ClientMessage.OperationResult.PrepareTimeout:
                    case ClientMessage.OperationResult.CommitTimeout:
                    case ClientMessage.OperationResult.ForwardTimeout:
                        return new InspectionResult(InspectionDecision.Retry);
                    case ClientMessage.OperationResult.WrongExpectedVersion:
                        var err = string.Format("Start transaction failed due to WrongExpectedVersion. Stream: {0}, Expected version: {1}, CorrID: {2}.",
                                                _stream,
                                                _expectedVersion,
                                                CorrelationId);
                        return new InspectionResult(InspectionDecision.NotifyError, new WrongExpectedVersionException(err));
                    case ClientMessage.OperationResult.StreamDeleted:
                        return new InspectionResult(InspectionDecision.NotifyError, new StreamDeletedException(_stream));
                    case ClientMessage.OperationResult.InvalidTransaction:
                        return new InspectionResult(InspectionDecision.NotifyError, new InvalidTransactionException());
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception e)
            {
                return new InspectionResult(InspectionDecision.NotifyError, e);
            }
        }

        public void Complete()
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                if (_result != null)
                    _source.SetResult(new EventStoreTransaction(_result.EventStreamId, _result.TransactionId, _parentConnection));
                else
                    _source.SetException(new NoResultException());
            }
        }

        public void Fail(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                _source.SetException(exception);
            }
        }

        public override string ToString()
        {
            return string.Format("Stream: {0}, ExpectedVersion: {1}, CorrelationId: {2}", _stream, _expectedVersion, CorrelationId);
        }
    }
}
