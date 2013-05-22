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
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations
{
    internal abstract class OperationBase<TResult, TResponse>: IClientOperation 
        where TResult: class
        where TResponse: class
    {
        private readonly TcpCommand _requestCommand;
        private readonly TcpCommand _responseCommand;
        private readonly UserCredentials _userCredentials;

        protected readonly ILogger Log;
        private readonly TaskCompletionSource<TResult> _source;
        private TResponse _response;
        private int _completed;

        protected abstract object CreateRequestDto();
        protected abstract InspectionResult InspectResponse(TResponse response);
        protected abstract TResult TransformResponse(TResponse response);

        protected OperationBase(ILogger log, TaskCompletionSource<TResult> source,
                                TcpCommand requestCommand, TcpCommand responseCommand,
                                UserCredentials userCredentials)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(source, "source");

            Log = log;
            _source = source;
            _requestCommand = requestCommand;
            _responseCommand = responseCommand;
            _userCredentials = userCredentials;
        }

        public TcpPackage CreateNetworkPackage(Guid correlationId)
        {
            return new TcpPackage(_requestCommand,
                                  _userCredentials != null ? TcpFlags.Authenticated : TcpFlags.None,
                                  correlationId,
                                  _userCredentials != null ? _userCredentials.Login : null,
                                  _userCredentials != null ? _userCredentials.Password : null,
                                  CreateRequestDto().Serialize());
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
                    case TcpCommand.NotAuthenticated: return InspectNotAuthenticated(package);
                    case TcpCommand.BadRequest: return InspectBadRequest(package);
                    case TcpCommand.NotHandled: return InspectNotHandled(package);
                    default: return InspectUnexpectedCommand(package, _responseCommand);
                }
            }
            catch (Exception e)
            {
                Fail(e);
                return new InspectionResult(InspectionDecision.EndOperation, null);
            }
        }

        protected void Succeed()
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

        public InspectionResult InspectNotAuthenticated(TcpPackage package)
        {
            string message = Helper.EatException(() => Helper.UTF8NoBom.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
            Fail(new NotAuthenticatedException(string.IsNullOrEmpty(message) ? "Authentication error" : message));
            return new InspectionResult(InspectionDecision.EndOperation, null);
        }

        public InspectionResult InspectBadRequest(TcpPackage package)
        {
            if (package.Command != TcpCommand.BadRequest)
                throw new ArgumentException(string.Format("Wrong command: {0}, expected: {1}.", package.Command, TcpCommand.BadRequest));
            string message = Helper.EatException(() => Helper.UTF8NoBom.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
            Fail(new ServerErrorException(string.IsNullOrEmpty(message) ? "<no message>" : message));
            return new InspectionResult(InspectionDecision.EndOperation, null);
        }

        public InspectionResult InspectNotHandled(TcpPackage package)
        {
            if (package.Command != TcpCommand.NotHandled)
                throw new ArgumentException(string.Format("Wrong command: {0}, expected: {1}.", package.Command, TcpCommand.NotHandled));
            var message = package.Data.Deserialize<ClientMessage.NotHandled>();

            switch (message.Reason)
            {
                case ClientMessage.NotHandled.NotHandledReason.NotReady:
                case ClientMessage.NotHandled.NotHandledReason.TooBusy:
                    return new InspectionResult(InspectionDecision.Retry, null);

                case ClientMessage.NotHandled.NotHandledReason.NotMaster:
                    var masterInfo = message.AdditionalInfo.Deserialize<ClientMessage.NotHandled.MasterInfo>();
                    return new InspectionResult(InspectionDecision.Reconnect, masterInfo.ExternalTcpEndPoint, masterInfo.ExternalSecureTcpEndPoint);

                default:
                    Log.Error("Unknown NotHandledReason: {0}.", message.Reason);
                    return new InspectionResult(InspectionDecision.Retry, null);
            }
        }

        public InspectionResult InspectUnexpectedCommand(TcpPackage package, TcpCommand expectedCommand)
        {
            if (package.Command == expectedCommand)
                throw new ArgumentException(string.Format("Command shouldn't be {0}.", package.Command));

            Log.Error("Unexpected TcpCommand received. Expected: {0}, actual: {1}.\n" +
                      "Flags: {2}, CorrelationId: {3}, TcpPackage Data Dump:\n{4}", 
                      expectedCommand, package.Command, package.Flags, package.CorrelationId, 
                      Helper.FormatBinaryDump(package.Data));
            if (Debugger.IsAttached) Debugger.Break(); else Debugger.Launch();

            Fail(new CommandNotExpectedException(expectedCommand.ToString(), package.Command.ToString()));
            return new InspectionResult(InspectionDecision.EndOperation, null);
        }
    }
}