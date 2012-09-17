// Copyright (c) 2012, Event Store Ltd
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
// Neither the name of the Event Store Ltd nor the names of its
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
using System.Net.Sockets;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class CreateStreamProcessor : ICmdProcessor
    {
        public string Usage { get { return "CR [<stream-id> [<metadata>]]"; } }
        public string Keyword { get { return "CR"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var eventStreamId = "test-stream";
            string metadata = null;

            if (args.Length > 0)
            {
                if (args.Length > 2)
                    return false;
                eventStreamId = args[0];
                if (args.Length > 1)
                    metadata = args[1];
            }

            context.IsAsync();

            var createStreamDto = new ClientMessageDto.CreateStream(
                    Guid.Empty,
                    eventStreamId,
                    Encoding.UTF8.GetBytes(metadata ?? string.Format("{{\"StreamName\": \"{0}\"}}", eventStreamId)));
            var package = new TcpPackage(TcpCommand.CreateStream, createStreamDto.Serialize());

            var sw = new Stopwatch();

            context.Client.CreateTcpConnection(
                    context,
                    connectionEstablished: conn =>
                    {
                        context.Log.Info("[{0}]: Trying to create stream '{1}'...", conn.EffectiveEndPoint, eventStreamId);
                        sw.Start();
                        conn.EnqueueSend(package.AsByteArray());
                    },
                    handlePackage: (conn, pkg) =>
                    {
                        if (pkg.Command != TcpCommand.CreateStreamCompleted)
                        {
                            context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                            return;
                        }

                        sw.Stop();

                        var dto = pkg.Data.Deserialize<ClientMessageDto.CreateStreamCompleted>();
                        if ((OperationErrorCode)dto.ErrorCode == OperationErrorCode.Success)
                        {
                            context.Log.Info("Successfully created stream '{0}'.", dto.EventStreamId);
                            PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword), (int)sw.ElapsedMilliseconds);
                        }
                        else
                        {
                            context.Log.Info("Error while creating stream {0}: {1} ({2}).",
                                             eventStreamId,
                                             dto.Error,
                                             (OperationErrorCode)dto.ErrorCode);
                        }

                        context.Log.Info("Create stream request took: {0}.", sw.Elapsed);
                        conn.Close();
                        context.Success();
                    },
                    connectionClosed: (connection, error) =>
                    {
                        if (error == SocketError.Success)
                            context.Success();
                        else
                            context.Fail();
                    });

            context.WaitForCompletion();
            return true;
        }
    }
}