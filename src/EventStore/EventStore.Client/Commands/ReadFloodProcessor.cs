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
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class ReadFloodProcessor : ICmdProcessor
    {
        public string Usage { get { return "RDFL [<clients> <requests> [<event-stream>]"; } }
        public string Keyword { get { return "RDFL"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            long requestsCnt = 5000;
            var eventStreamId = "test-stream";
            if (args.Length > 0)
            {
                if (args.Length != 2 && args.Length != 3)
                    return false;

                try
                {
                    clientsCnt = int.Parse(args[0]);
                    requestsCnt = long.Parse(args[1]);

                    if (args.Length == 3)
                        eventStreamId = args[2];
                }
                catch
                {
                    return false;
                }
            }

            ReadFlood(context, eventStreamId, clientsCnt, requestsCnt);
            return true;
        }

        private void ReadFlood(CommandProcessorContext context, string eventStreamId, int clientsCnt, long requestsCnt)
        {
            context.IsAsync();

            var clients = new List<TcpTypedConnection<byte[]>>();
            var threads = new List<Thread>();
            var autoResetEvent = new AutoResetEvent(false);
            var sw2 = new Stopwatch();

            long all = 0;

            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);

                int sent = 0;
                int received = 0;

                var client = context.Client.CreateTcpConnection(
                    context,
                    (conn, pkg) =>
                    {
                        Interlocked.Increment(ref received);

                        var localAll = Interlocked.Increment(ref all);
                        if (localAll % 1000 == 0) Console.Write(".");
                        if (localAll % 100000 == 0)
                        {
                            var elapsed = sw2.Elapsed;
                            sw2.Restart();
                            context.Log.Trace("\nDONE TOTAL {0} READS IN {1} ({2:0.0}/s).",
                                              localAll,
                                              elapsed,
                                              1000.0*100000/elapsed.TotalMilliseconds);
                        }
                        if (localAll == requestsCnt)
                            autoResetEvent.Set();
                    },
                    connectionClosed: (conn, err) =>
                    {
                        if (all < requestsCnt)
                            context.Fail(null, "Socket was closed, but not all requests were completed.");
                        else
                            context.Success();
                    });

                client.ConnectionClosed += (_, __) => context.Log.Debug("READS sent: {0}, received: {1}", sent, received);
                clients.Add(client);

                threads.Add(new Thread(() => 
                {
                    for (int j = 0; j < count; ++j)
                    {
                        var read = new ClientMessageDto.ReadEvent(Guid.NewGuid(), eventStreamId, 0);
                        var package = new TcpPackage(TcpCommand.ReadEvent, read.Serialize());
                        client.EnqueueSend(package.AsByteArray());
                        
                        Interlocked.Increment(ref sent);
                        while (sent - received > context.Client.Options.ReadWindow)
                            Thread.Sleep(1);
                    }
                }));
            }

            var sw = Stopwatch.StartNew();
            sw2.Start();
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

            context.Log.Info("Completed. READS done: {0}.", all);

            var reqPerSec = (requestsCnt + 0.0)/sw.ElapsedMilliseconds*1000;
            context.Log.Info("{0} requests completed in {1}ms ({2:0.00} reqs per sec).",
                             requestsCnt,
                             sw.ElapsedMilliseconds,
                             reqPerSec);

            PerfUtils.LogData(
                        Keyword,
                            PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
                                 PerfUtils.Col("requestsCnt", requestsCnt),
                                 PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
                        PerfUtils.Row(PerfUtils.Col("readsCnt", all))
            );

            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt),
                        (int)reqPerSec);
         
            context.Success();
        }
    }
}