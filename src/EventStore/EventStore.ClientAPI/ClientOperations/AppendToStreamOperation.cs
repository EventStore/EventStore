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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class AppendToStreamOperation : OperationBase<int, ClientMessage.WriteEventsCompleted>
    {
        private readonly bool _requireMaster;
        private readonly string _stream;
        private readonly int _expectedVersion;
        private readonly IEnumerable<EventData> _events;

        private bool _wasCommitTimeout;

        public AppendToStreamOperation(ILogger log, 
                                       TaskCompletionSource<int> source,
                                       bool requireMaster,
                                       string stream,
                                       int expectedVersion,
                                       IEnumerable<EventData> events,
                                       UserCredentials userCredentials)
            : base(log, source, TcpCommand.WriteEvents, TcpCommand.WriteEventsCompleted, userCredentials)
        {
            _requireMaster = requireMaster;
            _stream = stream;
            _expectedVersion = expectedVersion;
            _events = events;
        }

        protected override object CreateRequestDto()
        {
            var dtos = _events.Select(x => new ClientMessage.NewEvent(x.EventId.ToByteArray(), x.Type, x.IsJson ? 1 : 0, 0, x.Data, x.Metadata)).ToArray();
            return new ClientMessage.WriteEvents(_stream, _expectedVersion, dtos, _requireMaster);
        }

        protected override InspectionResult InspectResponse(ClientMessage.WriteEventsCompleted response)
        {
            switch (response.Result)
            {
                case ClientMessage.OperationResult.Success:
                    if (_wasCommitTimeout)
                        Log.Debug("IDEMPOTENT WRITE SUCCEEDED FOR {0}.", this);
                    Succeed();
                    return new InspectionResult(InspectionDecision.EndOperation, "Success");
                case ClientMessage.OperationResult.PrepareTimeout:
                    return new InspectionResult(InspectionDecision.Retry, "PrepareTimeout");
                case ClientMessage.OperationResult.ForwardTimeout:
                    return new InspectionResult(InspectionDecision.Retry, "ForwardTimeout");
                case ClientMessage.OperationResult.CommitTimeout:
                    _wasCommitTimeout = true;
                    return new InspectionResult(InspectionDecision.Retry, "CommitTimeout");
                case ClientMessage.OperationResult.WrongExpectedVersion:
                    var err = string.Format("Append failed due to WrongExpectedVersion. Stream: {0}, Expected version: {1}", _stream, _expectedVersion);
                    Fail(new WrongExpectedVersionException(err));
                    return new InspectionResult(InspectionDecision.EndOperation, "WrongExpectedVersion");
                case ClientMessage.OperationResult.StreamDeleted:
                    Fail(new StreamDeletedException(_stream));
                    return new InspectionResult(InspectionDecision.EndOperation, "StreamDeleted");
                case ClientMessage.OperationResult.InvalidTransaction:
                    Fail(new InvalidTransactionException());
                    return new InspectionResult(InspectionDecision.EndOperation, "InvalidTransaction");
                case ClientMessage.OperationResult.AccessDenied:
                    Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.", _stream)));
                    return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
                default:
                    throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
            }
        }

        protected override int TransformResponse(ClientMessage.WriteEventsCompleted response)
        {
            return response.LastEventNumber;
        }

        public override string ToString()
        {
            return string.Format("Stream: {0}, ExpectedVersion: {1}", _stream, _expectedVersion);
        }
    }
}