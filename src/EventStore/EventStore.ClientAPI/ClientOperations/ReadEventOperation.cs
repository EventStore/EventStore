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
    internal class ReadEventOperation : OperationBase<EventReadResult, ClientMessage.ReadEventCompleted>
    {
        private readonly string _stream;
        private readonly int _eventNumber;
        private readonly bool _resolveLinkTo;
        private readonly bool _requireMaster;

        public ReadEventOperation(ILogger log, TaskCompletionSource<EventReadResult> source,
                                  string stream, int eventNumber, bool resolveLinkTo, bool requireMaster, UserCredentials userCredentials)
            : base(log, source, TcpCommand.ReadEvent, TcpCommand.ReadEventCompleted, userCredentials)
        {
            _stream = stream;
            _eventNumber = eventNumber;
            _resolveLinkTo = resolveLinkTo;
            _requireMaster = requireMaster;
        }

        protected override object CreateRequestDto()
        {
            return new ClientMessage.ReadEvent(_stream, _eventNumber, _resolveLinkTo, _requireMaster);
        }

        protected override InspectionResult InspectResponse(ClientMessage.ReadEventCompleted response)
        {
            switch (response.Result)
            {
                case ClientMessage.ReadEventCompleted.ReadEventResult.Success:
                case ClientMessage.ReadEventCompleted.ReadEventResult.NotFound:
                case ClientMessage.ReadEventCompleted.ReadEventResult.NoStream:
                case ClientMessage.ReadEventCompleted.ReadEventResult.StreamDeleted:
                    Succeed();
                    return new InspectionResult(InspectionDecision.EndOperation);
                case ClientMessage.ReadEventCompleted.ReadEventResult.Error:
                    Fail(new ServerErrorException(string.IsNullOrEmpty(response.Error) ? "<no message>" : response.Error));
                    return new InspectionResult(InspectionDecision.EndOperation);
                case ClientMessage.ReadEventCompleted.ReadEventResult.AccessDenied:
                    Fail(new AccessDeniedException(string.Format("Read access denied for stream '{0}'.", _stream)));
                    return new InspectionResult(InspectionDecision.EndOperation);
                default: 
                    throw new Exception(string.Format("Unexpected ReadEventResult: {0}.", response.Result));
            }
        }

        protected override EventReadResult TransformResponse(ClientMessage.ReadEventCompleted response)
        {
            return new EventReadResult(Convert(response.Result), _stream, _eventNumber, response.Event);
        }


        private static EventReadStatus Convert(ClientMessage.ReadEventCompleted.ReadEventResult result)
        {
            switch (result)
            {
                case ClientMessage.ReadEventCompleted.ReadEventResult.Success:
                    return EventReadStatus.Success;
                case ClientMessage.ReadEventCompleted.ReadEventResult.NotFound:
                    return EventReadStatus.NotFound;
                case ClientMessage.ReadEventCompleted.ReadEventResult.NoStream:
                    return EventReadStatus.NoStream;
                case ClientMessage.ReadEventCompleted.ReadEventResult.StreamDeleted:
                    return EventReadStatus.StreamDeleted;
                default:
                    throw new Exception(string.Format("Unexpected ReadEventResult: {0}.", result));
            }
        }

        public override string ToString()
        {
            return string.Format("Stream: {0}, EventNumber: {1}, ResolveLinkTo: {2}, RequireMaster: {3}",
                                 _stream, _eventNumber, _resolveLinkTo, _requireMaster);
        }
    }
}