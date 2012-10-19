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
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.System;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class DeleteStreamOperation : IClientOperation
    {
        private readonly TaskCompletionSource<object> _source;
        private ClientMessages.DeleteStreamCompleted _result;

        private Guid _correlationId;
        private readonly object _corrIdLock = new object();

        private readonly string _stream;
        private readonly int _expectedVersion;

        public Guid CorrelationId
        {
            get
            {
                lock (_corrIdLock)
                    return _correlationId;
            }
        }

        public DeleteStreamOperation(TaskCompletionSource<object> source,
                                     Guid correlationId, 
                                     string stream, 
                                     int expectedVersion)
        {
            _source = source;

            _correlationId = correlationId;
            _stream = stream;
            _expectedVersion = expectedVersion;
        }

        public TcpPackage CreateNetworkPackage()
        {
            lock (_corrIdLock)
            {
                var dto = new ClientMessages.DeleteStream(_stream, _expectedVersion);
                return new TcpPackage(TcpCommand.DeleteStream, _correlationId, dto.Serialize());
            }
        }

        public void SetRetryId(Guid corrId)
        {
            lock (_corrIdLock)
                _correlationId = corrId;
        }

        public InspectionResult InspectPackage(TcpPackage package)
        {
            try
            {
                if (package.Command != TcpCommand.DeleteStreamCompleted)
                {
                    return new InspectionResult(InspectionDecision.NotifyError,
                                                new CommandNotExpectedException(TcpCommand.DeleteStreamCompleted.ToString(), 
                                                                                package.Command.ToString()));
                }

                var data = package.Data;
                var dto = data.Deserialize<ClientMessages.DeleteStreamCompleted>();
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
                        var err = string.Format("Delete stream failed due to WEV. Stream : {0}, Expected ver : {1}, CorrID : {2}",
                                                _stream,
                                                _expectedVersion,
                                                CorrelationId);
                        return new InspectionResult(InspectionDecision.NotifyError, new WrongExpectedVersionException(err));
                    case OperationErrorCode.StreamDeleted:
                        return new InspectionResult(InspectionDecision.NotifyError, new StreamDeletedException());
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
            if (_result != null)
                _source.SetResult(null);
            else
                _source.SetException(new NoResultException());
        }

        public void Fail(Exception exception)
        {
            _source.SetException(exception);
        }

        public override string ToString()
        {
            return string.Format("Stream: {0}, ExpectedVersion: {1}, CorrelationId: {2}", _stream, _expectedVersion, CorrelationId);
        }
    }
}