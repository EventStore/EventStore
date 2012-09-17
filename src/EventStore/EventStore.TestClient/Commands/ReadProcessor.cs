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
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class ReadProcessor : ICmdProcessor
    {
        public string Usage { get { return "RD [<stream-id> [<from-number>]]"; } }
        public string Keyword { get { return "RD"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var eventStreamId = "test-stream";
            var fromNumber = 0;

            if (args.Length > 0)
            {
                if (args.Length > 2)
                    return false;

                eventStreamId = args[0];

                if (args.Length == 2)
                    fromNumber = int.Parse(args[1]);
            }

            context.IsAsync();

            var corrid = Guid.NewGuid();
            var readDto = new ClientMessageDto.ReadEvent(corrid, eventStreamId, fromNumber);
            var package = new TcpPackage(TcpCommand.ReadEvent, corrid, readDto.Serialize());

            var sw = new Stopwatch();

            context.Client.CreateTcpConnection(
                context,
                connectionEstablished: conn =>
                {
                    context.Log.Info("[{0}]: Reading...", conn.EffectiveEndPoint);
                    sw.Start();
                    conn.EnqueueSend(package.AsByteArray());
                },
                handlePackage: (conn, pkg) =>
                {
                    if (pkg.Command != TcpCommand.ReadEventCompleted)
                    {
                        context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                        return;
                    }

                    sw.Stop();

                    var dto = pkg.Data.Deserialize<ClientMessageDto.ReadEventCompleted>();

                    context.Log.Info("READ events from <{0}>:\n\n"
                                     + "\tCorrelationId: {1}\n"
                                     + "\tEventStreamId: {2}\n"
                                     + "\tEventNumber:   {3}\n"
                                     + "\tReadResult:    {4}\n"
                                     + "\tEventType:     {5}\n"
                                     + "\tData:          {6}\n"
                                     + "\tMetadata:      {7}\n",
                                     eventStreamId,
                                     dto.CorrelationId,
                                     dto.EventStreamId,
                                     dto.EventNumber,
                                     (SingleReadResult) dto.Result,
                                     dto.EventType,
                                     Encoding.UTF8.GetString(dto.Data ?? new byte[0]),
                                     Encoding.UTF8.GetString(dto.Metadata ?? new byte[0]));

                    context.Log.Info("Read request took: {0}.", sw.Elapsed);

                    PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword), (int)sw.ElapsedMilliseconds);

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
