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
using System.Threading;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class PingFloodWaitingProcessor : ICmdProcessor
    {
        public string Usage { get { return "PINGFLW [<clients> <messages>]"; } }
        public string Keyword { get { return "PINGFLW"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            long requestsCnt = 100000;
            if (args.Length > 0)
            {
                if (args.Length != 2)
                    return false;
                try
                {
                    clientsCnt = int.Parse(args[0]);
                    requestsCnt = long.Parse(args[1]);
                }
                catch
                {
                    return false;
                }
            }

            PingFloodWaiting(context, clientsCnt, requestsCnt);
            return true;
        }

        private void PingFloodWaiting(CommandProcessorContext context, int clientsCnt, long requestsCnt)
        {
            context.IsAsync();

            var clients = new List<TcpTypedConnection<byte[]>>();
            var threads = new List<Thread>();
            var doneEvent = new ManualResetEventSlim(false);
            var clientsDone = 0;
            long all = 0;
            for (int i = 0; i < clientsCnt; i++)
            {
                var autoResetEvent = new AutoResetEvent(false);
                var client = context.Client.CreateTcpConnection(
                    context,
                    (_, __) =>
                    {
                        Interlocked.Increment(ref all);
                        autoResetEvent.Set();
                    },
                    connectionClosed: (conn, err) => context.Fail(reason: "Connection was closed prematurely."));
                clients.Add(client);

                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
                threads.Add(new Thread(() =>
                {
                    for (int j = 0; j < count; ++j)
                    {
                        var package = new TcpPackage(TcpCommand.Ping, Guid.NewGuid(), null);
                        client.EnqueueSend(package.AsByteArray());
                        autoResetEvent.WaitOne();
                    }
                    if (Interlocked.Increment(ref clientsDone) == clientsCnt)
                    {
                        context.Success();
                        doneEvent.Set();
                    }
                }) { IsBackground = true });
            }

            var sw = Stopwatch.StartNew();
            threads.ForEach(thread => thread.Start());
            doneEvent.Wait();
            sw.Stop();
            clients.ForEach(x => x.Close());

            var reqPerSec = (all + 0.0)/sw.ElapsedMilliseconds*1000;
            context.Log.Info("{0} requests completed in {1}ms ({2:0.00} reqs per sec).", all, sw.ElapsedMilliseconds, reqPerSec);
            PerfUtils.LogData(Keyword,
                              PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
                                            PerfUtils.Col("requestsCnt", requestsCnt),
                                            PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)));
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt), (int) reqPerSec);
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword), (int) (sw.ElapsedMilliseconds/all));

            if (Interlocked.Read(ref all) == requestsCnt)
                context.Success();
            else
                context.Fail();
        }
    }
}