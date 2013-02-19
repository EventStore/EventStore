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
using System.Text;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations
{
    internal static class OperationHelpers
    {
        public static InspectionResult InspectBadRequest(this TcpPackage package)
        {
            if (package.Command != TcpCommand.BadRequest)
                throw new ArgumentException(string.Format("Wrong command: {0}, expected: {1}.", package.Command, TcpCommand.BadRequest));
            string message;
            try
            {
                message = Encoding.UTF8.GetString(package.Data.Array, package.Data.Offset, package.Data.Count);
            }
            catch (Exception exc)
            {
                message = exc.ToString();
            }
            return new InspectionResult(InspectionDecision.NotifyError,
                                        new ServerErrorException(string.IsNullOrEmpty(message) ? "<no message>" : message));
        }

        public static InspectionResult InspectDeniedToRoute(this TcpPackage package)
        {
            if (package.Command != TcpCommand.DeniedToRoute)
                throw new ArgumentException(string.Format("Wrong command: {0}, expected: {1}.", package.Command, TcpCommand.DeniedToRoute));
            var route = package.Data.Deserialize<ClientMessage.DeniedToRoute>();
            return new InspectionResult(InspectionDecision.Reconnect, data: route.ExternalTcpEndPoint);
        }

        public static InspectionResult InspectUnexpectedCommand(this TcpPackage package, TcpCommand expectedCommand)
        {
            if (package.Command == expectedCommand)
                throw new ArgumentException(string.Format("Command shouldn't be {0}.", package.Command));
            return new InspectionResult(InspectionDecision.NotifyError,
                                        new CommandNotExpectedException(expectedCommand.ToString(), package.Command.ToString()));
        }
    }
}