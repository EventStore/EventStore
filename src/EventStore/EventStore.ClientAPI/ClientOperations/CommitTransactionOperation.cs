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
    internal class CommitTransactionOperation : IClientOperation
    {
        private readonly TaskCompletionSource<object> _source;
        private ClientMessages.TransactionCommitCompleted _result;
        private int _completed;

        private Guid _corrId;
        private readonly object _corrIdLock = new object();

        private readonly bool _forward;
        private readonly long _transactionId;
        private readonly string _stream;

        public Guid CorrelationId
        {
            get
            {
                lock (_corrIdLock)
                    return _corrId;
            }
        }

        public CommitTransactionOperation(TaskCompletionSource<object> source,
                                          Guid corrId,
                                          bool forward,
                                          long transactionId,
                                          string stream)
        {
            _source = source;

            _corrId = corrId;
            _forward = forward;
            _transactionId = transactionId;
            _stream = stream;
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
                var commit = new ClientMessages.TransactionCommit(_transactionId, _stream, _forward);
                return new TcpPackage(TcpCommand.TransactionCommit, _corrId, commit.Serialize());
            }
        }

        public InspectionResult InspectPackage(TcpPackage package)
        {
            try
            {
                if (package.Command == TcpCommand.DeniedToRoute)
                {
                    var route = package.Data.Deserialize<ClientMessages.DeniedToRoute>();
                    return new InspectionResult(InspectionDecision.Reconnect,
                                                data: new EndpointsPair(route.ExternalTcpEndPoint,
                                                                        route.ExternalHttpEndPoint));
                }
                if (package.Command != TcpCommand.TransactionCommitCompleted)
                {
                    return new InspectionResult(InspectionDecision.NotifyError,
                                                new CommandNotExpectedException(TcpCommand.TransactionCommitCompleted.ToString(),
                                                                                package.Command.ToString()));
                }

                var data = package.Data;
                var dto = data.Deserialize<ClientMessages.TransactionCommitCompleted>();
                _result = dto;

                switch ((OperationErrorCode)dto.ErrorCode)
                {
                    case OperationErrorCode.Success:
                        return new InspectionResult(InspectionDecision.Succeed);
                    case OperationErrorCode.PrepareTimeout:
                    case OperationErrorCode.CommitTimeout:
                    case OperationErrorCode.ForwardTimeout:
                        return new InspectionResult(InspectionDecision.Retry);
                    case OperationErrorCode.WrongExpectedVersion:
                        var err = string.Format("Commit transaction failed due to WrongExpectedVersion. Stream: {0}, TransactionID: {1}, CorrID: {2}.",
                                                _stream,
                                                _transactionId,
                                                CorrelationId);
                        return new InspectionResult(InspectionDecision.NotifyError, new WrongExpectedVersionException(err));
                    case OperationErrorCode.StreamDeleted:
                        return new InspectionResult(InspectionDecision.NotifyError, new StreamDeletedException(_stream));
                    case OperationErrorCode.InvalidTransaction:
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
                    _source.SetResult(null);
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
            return string.Format("TransactionId: {0}, Stream: {1}, CorrelationId: {2}", _transactionId, _stream, CorrelationId);
        }
    }
}
