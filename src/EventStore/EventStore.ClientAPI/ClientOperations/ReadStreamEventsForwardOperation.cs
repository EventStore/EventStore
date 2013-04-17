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
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class ReadStreamEventsForwardOperation : OperationBase<StreamEventsSlice, ClientMessage.ReadStreamEventsCompleted>
    {
        private readonly string _stream;
        private readonly int _fromEventNumber;
        private readonly int _maxCount;
        private readonly bool _resolveLinkTos;

        public ReadStreamEventsForwardOperation(ILogger log, 
                                                TaskCompletionSource<StreamEventsSlice> source,
                                                string stream,
                                                int fromEventNumber,
                                                int maxCount,
                                                bool resolveLinkTos)
            : base(log, source, TcpCommand.ReadStreamEventsForward, TcpCommand.ReadStreamEventsForwardCompleted)
        {
            _stream = stream;
            _fromEventNumber = fromEventNumber;
            _maxCount = maxCount;
            _resolveLinkTos = resolveLinkTos;
        }

        protected override object CreateRequestDto()
        {
            return new ClientMessage.ReadStreamEvents(_stream, _fromEventNumber, _maxCount, _resolveLinkTos);
        }

        protected override InspectionResult InspectResponse(ClientMessage.ReadStreamEventsCompleted response)
        {
            switch (response.Result)
            {
                case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.Success:
                case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.StreamDeleted:
                case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.NoStream:
                    Succeed();
                    return new InspectionResult(InspectionDecision.EndOperation);
                case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.Error:
                    Fail(new ServerErrorException("Unexpected error occurred on server. See server logs for more information."));
                    return new InspectionResult(InspectionDecision.EndOperation);
                case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.AccessDenied:
                    Fail(new AccessDeniedException(string.Format("Read access denied for stream '{0}'.", _stream)));
                    return new InspectionResult(InspectionDecision.EndOperation);
                default:
                    throw new ArgumentOutOfRangeException(string.Format("Unexpected ReadStreamResult: {0}.", response.Result));
            }
        }

        protected override StreamEventsSlice TransformResponse(ClientMessage.ReadStreamEventsCompleted response)
        {
            return new StreamEventsSlice(StatusCode.Convert(response.Result),
                                         _stream,
                                         _fromEventNumber,
                                         ReadDirection.Forward,
                                         response.Events,
                                         response.NextEventNumber,
                                         response.LastEventNumber,
                                         response.IsEndOfStream);
        }

        public override string ToString()
        {
            return string.Format("Stream: {0}, FromEventNumber: {1}, MaxCount: {2}, ResolveLinkTos: {3}",
                                 _stream,
                                 _fromEventNumber,
                                 _maxCount,
                                 _resolveLinkTos);
        }
    }
}