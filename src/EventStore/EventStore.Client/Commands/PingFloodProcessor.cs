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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Core.Services.Transport;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class PingFloodProcessor : ICmdProcessor
    {
        private static readonly byte[] Payload = new byte[0];

        public string Usage { get { return "PINGFL [<clients> <messages>]"; } }
        public string Keyword { get { return "PINGFL"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            int requestsCnt = 1000000;
            if (args.Length > 0)
            {
                if (args.Length != 2)
                    return false;

                try
                {
                    clientsCnt = int.Parse(args[0]);
                    requestsCnt = int.Parse(args[1]);
                }
                catch
                {
                    return false;
                }
            }

            PingFlood(context, clientsCnt, requestsCnt);
            return true;
        }

        private void PingFlood(CommandProcessorContext context, int clientsCnt, int requestsCnt)
        {
            context.IsAsync();

            var autoResetEvent = new AutoResetEvent(false);
            var clients = new List<TcpTypedConnection<byte[]>>();
            var threads = new List<Thread>();
            var all = 0;

            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
                int sent = 0;
                int received = 0;

                var client = context.Client.CreateTcpConnection(
                    context,
                    (conn, msg) =>
                    {
                        Interlocked.Increment(ref received);
                        var pongs = Interlocked.Increment(ref all);
                        if (pongs % 10000 == 0)
                            Console.Write('.');
                        if (pongs == requestsCnt)
                            autoResetEvent.Set();
                    },
                    connectionClosed: (conn, err) =>
                    {
                        if (all < requestsCnt)
                            context.Fail(null, "Socket was closed, but not all requests were completed.");
                        else
                            context.Success();
                    });

                clients.Add(client);

                threads.Add(new Thread(() =>
                {
                    for (int j = 0; j < count; ++j)
                    {
                        var package = new TcpPackage(TcpCommand.Ping, Payload);
                        client.EnqueueSend(package.AsByteArray());
                        Interlocked.Increment(ref sent);

                        while (sent - received > context.Client.Options.PingWindow)
                            Thread.Sleep(1);
                    }
                }));
            }

            var sw = Stopwatch.StartNew();
            foreach (var thread in threads)
            {
                thread.IsBackground = true;
                thread.Start();
            }

            autoResetEvent.WaitOne();
            sw.Stop();

            foreach (var client in clients)
            {
                client.Close();
            }

            var reqPerSec = (requestsCnt + 0.0)/sw.ElapsedMilliseconds*1000;
            context.Log.Info("{0} requests completed in {1}ms ({2:0.00} reqs per sec).",
                             requestsCnt,
                             sw.ElapsedMilliseconds,
                             reqPerSec);

            PerfUtils.LogData(
                    Keyword,
                    PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
                            PerfUtils.Col("requestsCnt", requestsCnt),
                            PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds))
                );

            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt),
                                    (int)reqPerSec);

            context.Success();
        }
    }
}