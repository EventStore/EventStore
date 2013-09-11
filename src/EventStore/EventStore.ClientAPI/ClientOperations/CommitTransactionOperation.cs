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
    internal class CommitTransactionOperation : OperationBase<int, ClientMessage.TransactionCommitCompleted>
    {
        private readonly bool _requireMaster;
        private readonly long _transactionId;

        public CommitTransactionOperation(ILogger log, TaskCompletionSource<int> source,
                                          bool requireMaster, long transactionId, UserCredentials userCredentials)
            : base(log, source, TcpCommand.TransactionCommit, TcpCommand.TransactionCommitCompleted, userCredentials)
        {
            _requireMaster = requireMaster;
            _transactionId = transactionId;
        }

        protected override object CreateRequestDto()
        {
            return new ClientMessage.TransactionCommit(_transactionId, _requireMaster);
        }

        protected override InspectionResult InspectResponse(ClientMessage.TransactionCommitCompleted response)
        {
            switch (response.Result)
            {
                case ClientMessage.OperationResult.Success:
                    Succeed();
                    return new InspectionResult(InspectionDecision.EndOperation, "Success");
                case ClientMessage.OperationResult.PrepareTimeout:
                    return new InspectionResult(InspectionDecision.Retry, "PrepareTimeout");
                case ClientMessage.OperationResult.CommitTimeout:
                    return new InspectionResult(InspectionDecision.Retry, "CommitTimeout");
                case ClientMessage.OperationResult.ForwardTimeout:
                    return new InspectionResult(InspectionDecision.Retry, "ForwardTimeout");
                case ClientMessage.OperationResult.WrongExpectedVersion:
                    var err = string.Format("Commit transaction failed due to WrongExpectedVersion. TransactionID: {0}.", _transactionId);
                    Fail(new WrongExpectedVersionException(err));
                    return new InspectionResult(InspectionDecision.EndOperation, "WrongExpectedVersion");
                case ClientMessage.OperationResult.StreamDeleted:
                    Fail(new StreamDeletedException());
                    return new InspectionResult(InspectionDecision.EndOperation, "StreamDeleted");
                case ClientMessage.OperationResult.InvalidTransaction:
                    Fail(new InvalidTransactionException());
                    return new InspectionResult(InspectionDecision.EndOperation, "InvalidTransaction");
                case ClientMessage.OperationResult.AccessDenied:
                    Fail(new AccessDeniedException("Write access denied."));
                    return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
                default:
                    throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
            }
        }

        protected override int TransformResponse(ClientMessage.TransactionCommitCompleted response)
        {
            return response.LastEventNumber;
        }

        public override string ToString()
        {
            return string.Format("TransactionId: {0}", _transactionId);
        }
    }
}
