﻿// Copyright (c) 2012, Event Store LLP
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
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class ReadProcessor : ICmdProcessor
    {
        public string Usage { get { return "RD [<stream-id> [<from-number> [<only-if-master>]]]"; } }
        public string Keyword { get { return "RD"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var eventStreamId = "test-stream";
            var fromNumber = 0;
            const bool resolveLinkTos = false;
            var requireMaster = false;

            if (args.Length > 0)
            {
                if (args.Length > 3)
                    return false;
                eventStreamId = args[0];
                if (args.Length >= 2)
                    fromNumber = int.Parse(args[1]);
                if (args.Length >= 3)
                    requireMaster = bool.Parse(args[2]);
            }

            context.IsAsync();

            var sw = new Stopwatch();
            context.Client.CreateTcpConnection(
                context,
                connectionEstablished: conn =>
                {
                    context.Log.Info("[{0}, L{1}]: Reading...", conn.RemoteEndPoint, conn.LocalEndPoint);
                    var readDto = new TcpClientMessageDto.ReadEvent(eventStreamId, fromNumber, resolveLinkTos, requireMaster);
                    var package = new TcpPackage(TcpCommand.ReadEvent, Guid.NewGuid(), readDto.Serialize()).AsByteArray();
                    sw.Start();
                    conn.EnqueueSend(package);
                },
                handlePackage: (conn, pkg) =>
                {
                    sw.Stop();
                    context.Log.Info("Read request took: {0}.", sw.Elapsed);

                    if (pkg.Command != TcpCommand.ReadEventCompleted)
                    {
                        context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                        return;
                    }

                    var dto = pkg.Data.Deserialize<TcpClientMessageDto.ReadEventCompleted>();
                    context.Log.Info("READ events from <{0}>:\n\n"
                                     + "\tEventStreamId: {1}\n"
                                     + "\tEventNumber:   {2}\n"
                                     + "\tReadResult:    {3}\n"
                                     + "\tEventType:     {4}\n"
                                     + "\tData:          {5}\n"
                                     + "\tMetadata:      {6}\n",
                                     eventStreamId,
                                     eventStreamId,
                                     dto.Event.Event.EventNumber,
                                     (ReadEventResult)dto.Result,
                                     dto.Event.Event.EventType,
                                     Helper.UTF8NoBom.GetString(dto.Event.Event.Data ?? new byte[0]),
                                     Helper.UTF8NoBom.GetString(dto.Event.Event.Metadata ?? new byte[0]));


                    if (dto.Result == TcpClientMessageDto.ReadEventCompleted.ReadEventResult.Success)
                    {
                        PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword), (int)Math.Round(sw.Elapsed.TotalMilliseconds));
                        context.Success();
                    }
                    else
                        context.Fail();

                    conn.Close();
                },
                connectionClosed: (connection, error) => context.Fail(reason: "Connection was closed prematurely."));

            return true;
        }
    }
}
