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
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class WriteFloodWaitingProcessor : ICmdProcessor
    {
        public string Usage { get { return "WRFLW [<clients> <requests> [payload-size]]"; } }
        public string Keyword { get { return "WRFLW"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            int requestsCnt = 5000;
            int payloadSize = 256 + 100;
            if (args.Length > 0)
            {
                if (args.Length > 3 || args.Length < 2)
                    return false;

                try
                {
                    clientsCnt = int.Parse(args[0]);
                    requestsCnt = int.Parse(args[1]);
                    if (args.Length == 3)
                        payloadSize = int.Parse(args[2]);
                }
                catch
                {
                    return false;
                }
            }

            WriteFlood(context, clientsCnt, requestsCnt, payloadSize);
            return true;
        }

        private void WriteFlood(CommandProcessorContext context, int clientsCnt, int requestsCnt, int payloadSize)
        {
            context.IsAsync();

            var dataSize = Math.Max(0, payloadSize - 100);
            var metadataSize = Math.Min(100, payloadSize);

            var clients = new List<TcpTypedConnection<byte[]>>();
            var threads = new List<Thread>();
            var doneEvent = new ManualResetEventSlim(false);
            var succ = 0;
            var fail = 0;
            var all = 0;
            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
                var autoEvent = new AutoResetEvent(false);
                var eventStreamId = "es" + Guid.NewGuid();
                var received = 0;
                var client = context.Client.CreateTcpConnection(
                    context,
                    (conn, pkg) =>
                    {
                        if (pkg.Command != TcpCommand.WriteEventsCompleted)
                        {
                            context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                            return;
                        }

                        Interlocked.Increment(ref received);
                        var dto = pkg.Data.Deserialize<TcpClientMessageDto.WriteEventsCompleted>();
                        if (dto.Result == TcpClientMessageDto.OperationResult.Success)
                        {
                            if (Interlocked.Increment(ref succ)%1000 == 0) Console.Write(".");
                        }
                        else 
                        {
                            if (Interlocked.Increment(ref fail)%1000 == 0) Console.Write("#");
                        }

                        if (Interlocked.Increment(ref all) == requestsCnt)
                        {
                            context.Success();
                            doneEvent.Set();
                        }
                        autoEvent.Set();
                    },
                    connectionClosed: (conn, err) => context.Fail(reason: "Connection was closed prematurely."));
                clients.Add(client);

                threads.Add(new Thread(() =>
                {
                    for (int j = 0; j < count; ++j)
                    {
                        var write = new TcpClientMessageDto.WriteEvents(
                            eventStreamId,
                            ExpectedVersion.Any,
                            new[]
                            {
                                new TcpClientMessageDto.NewEvent(Guid.NewGuid().ToByteArray(),
                                                                 "TakeSomeSpaceEvent",
                                                                 0,0,
                                                                 Common.Utils.Helper.UTF8NoBom.GetBytes("DATA" + new string('*', dataSize)),
                                                                 Common.Utils.Helper.UTF8NoBom.GetBytes("METADATA" + new string('$', metadataSize)))
                            },
                            false);
                        var package = new TcpPackage(TcpCommand.WriteEvents, Guid.NewGuid(), write.Serialize());
                        client.EnqueueSend(package.AsByteArray());
                        autoEvent.WaitOne();
                    }
                }) { IsBackground = true });
            }

            var sw = Stopwatch.StartNew();
            threads.ForEach(thread => thread.Start());
            doneEvent.Wait();
            sw.Stop();
            clients.ForEach(client => client.Close());

            var reqPerSec = (all + 0.0)/sw.ElapsedMilliseconds*1000;
            context.Log.Info("Completed. Successes: {0}, failures: {1}", succ, fail);
            context.Log.Info("{0} requests completed in {1}ms ({2:0.00} reqs per sec, latency: {3:0.00} ms).",
                             all, sw.ElapsedMilliseconds, reqPerSec, (sw.Elapsed.TotalMilliseconds + 0.0) / requestsCnt);
            PerfUtils.LogData(Keyword,
                              PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
                                            PerfUtils.Col("requestsCnt", requestsCnt),
                                            PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
                              PerfUtils.Row(PerfUtils.Col("successes", succ), PerfUtils.Col("failures", fail)));
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt), (int) reqPerSec);
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt), (int)(100.0 * fail / (fail + succ)));
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword), (int) Math.Round(sw.Elapsed.TotalMilliseconds/requestsCnt));

            if (succ != requestsCnt)
                context.Fail(reason: "There were errors or not all requests completed.");
            else
                context.Success();
        }
    }
}
