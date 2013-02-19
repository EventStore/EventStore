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
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations
{
    internal abstract class OperationBase<TResult, TResponse>: IClientOperation 
        where TResult: class
        where TResponse: class
    {
        public Guid CorrelationId
        {
            get
            {
                lock (_corrIdLock)
                {
                    return _correlationId;
                }
            }
        }

        private readonly TcpCommand _requestCommand;
        private readonly TcpCommand _responseCommand;

        private readonly TaskCompletionSource<TResult> _source;
        private TResponse _response;
        private int _completed;
        private Guid _correlationId;
        private readonly object _corrIdLock = new object();

        protected abstract object CreateRequestDto();
        protected abstract InspectionResult InspectResponse(TResponse response);
        protected abstract TResult TransformResponse(TResponse response);

        protected OperationBase(TaskCompletionSource<TResult> source, Guid correlationId, TcpCommand requestCommand, TcpCommand responseCommand)
        {
            Ensure.NotNull(source, "source");
            Ensure.NotEmptyGuid(correlationId, "correlationId");

            _source = source;
            _correlationId = correlationId;
            _requestCommand = requestCommand;
            _responseCommand = responseCommand;
        }

        public void SetRetryId(Guid correlationId)
        {
            Ensure.NotEmptyGuid(correlationId, "correlationId");
            lock (_corrIdLock)
            {
                _correlationId = correlationId;
            }
        }

        public TcpPackage CreateNetworkPackage()
        {
            Guid corrId;
            lock (_corrIdLock)
            {
                corrId = _correlationId;
            }
            return new TcpPackage(_requestCommand, corrId, CreateRequestDto().Serialize());
        }

        public virtual InspectionResult InspectPackage(TcpPackage package)
        {
            try
            {
                if (package.Command == _responseCommand)
                {
                    _response = package.Data.Deserialize<TResponse>();
                    return InspectResponse(_response);
                }
                switch (package.Command)
                {
                    case TcpCommand.BadRequest: return package.InspectBadRequest();
                    case TcpCommand.DeniedToRoute: return package.InspectDeniedToRoute();
                    default: return package.InspectUnexpectedCommand(TcpCommand.TransactionCommitCompleted);
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
                if (_response != null)
                    _source.SetResult(TransformResponse(_response));
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
    }
}