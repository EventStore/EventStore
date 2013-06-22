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
using System.Linq;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class MultiWriteProcessor : ICmdProcessor
    {
        public string Usage { get { return "MWR [<write-count=10> [<stream=test-stream> [<expected-version=ANY>]]"; } }
        public string Keyword { get { return "MWR"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            const string data = "test-data";
            var eventStreamId = "test-stream";
            var writeCount = 10;
            var expectedVersion = ExpectedVersion.Any;

            if (args.Length > 0)
            {
                if (args.Length > 3)
                    return false;
                writeCount = int.Parse(args[0]);
                if (args.Length >= 2)
                    eventStreamId = args[1];
                if (args.Length >= 3)
                    expectedVersion = args[2].Trim().ToUpper() == "ANY" ? ExpectedVersion.Any : int.Parse(args[2].Trim());
            }

            context.IsAsync();
            var sw = new Stopwatch();
            context.Client.CreateTcpConnection(
                context,
                connectionEstablished: conn =>
                {
                    context.Log.Info("[{0}, L{1}]: Writing...", conn.RemoteEndPoint, conn.LocalEndPoint);
                    var writeDto = new TcpClientMessageDto.WriteEvents(
                        eventStreamId,
                        expectedVersion,
                        Enumerable.Range(0, writeCount).Select(x => new TcpClientMessageDto.NewEvent(Guid.NewGuid().ToByteArray(),
                                                                                                     "type",
                                                                                                     false,
                                                                                                     Helper.UTF8NoBom.GetBytes(data),
                                                                                                     new byte[0])).ToArray(),
                        true);
                    var package = new TcpPackage(TcpCommand.WriteEvents, Guid.NewGuid(), writeDto.Serialize()).AsByteArray();
                    sw.Start();
                    conn.EnqueueSend(package);
                },
                handlePackage: (conn, pkg) =>
                {
                    sw.Stop();
                    context.Log.Info("Write request took: {0}.", sw.Elapsed);

                    if (pkg.Command != TcpCommand.WriteEventsCompleted)
                    {
                        context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                        return;
                    }

                    var dto = pkg.Data.Deserialize<TcpClientMessageDto.WriteEventsCompleted>();
                    if (dto.Result == TcpClientMessageDto.OperationResult.Success)
                    {
                        context.Log.Info("Successfully written {0} events.", writeCount);
                        PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword), (int)sw.ElapsedMilliseconds);
                        context.Success();
                    }
                    else
                    {
                        context.Log.Info("Error while writing: {0}.", dto.Result);
                        context.Fail();
                    }
                    conn.Close();
                },
                connectionClosed: (connection, error) => context.Fail(reason: "Connection was closed prematurely."));

            context.WaitForCompletion();
            return true;
        }
    }
}