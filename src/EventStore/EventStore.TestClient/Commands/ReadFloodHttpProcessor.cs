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
using System.Threading;
using EventStore.Transport.Http;
using EventStore.Common.Utils;
using EventStore.Transport.Http.Client;

namespace EventStore.TestClient.Commands
{
    internal class ReadFloodHttpProcessor : ICmdProcessor
    {
        public string Usage { get { return "RDFLH [<clients> <requests> [<event-stream>]"; } }
        public string Keyword { get { return "RDFLH"; } }

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

            var threads = new List<Thread>();
            var doneEvent = new ManualResetEventSlim(false);
            long succ = 0;
            long fail = 0;
            long all = 0;

            var sw2 = new Stopwatch();
            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
                long sent = 0;
                long received = 0;
                threads.Add(new Thread(() =>
                {
                    var client = new HttpAsyncClient();
                    var url = context.Client.HttpEndpoint.ToHttpUrl("/streams/{0}/{1}?format=json", eventStreamId, 0);

                    Action onReceived = () =>
                    {
                        Interlocked.Increment(ref received);
                        var localAll = Interlocked.Increment(ref all);
                        if (localAll % 10000 == 0)
                        {
                            var elapsed = sw2.Elapsed;
                            sw2.Restart();
                            context.Log.Trace("\nDONE TOTAL {0} READS IN {1} ({2:0.0}/s).", localAll, elapsed, 1000.0 * 10000 / elapsed.TotalMilliseconds);
                        }
                        if (localAll == requestsCnt)
                            doneEvent.Set();
                    };

                    Action<HttpResponse> onSuccess = response =>
                    {
                        if (Interlocked.Increment(ref succ) % 100 == 0) Console.Write(".");
                        onReceived();
                    };

                    Action<Exception> onException = exc =>
                    {
                        context.Log.ErrorException(exc, "Error during GET");
                        if (Interlocked.Increment(ref fail) % 100 == 0) Console.Write("#");
                        onReceived();
                    };

                    for (int j = 0; j < count; ++j)
                    {
                        client.Get(url, TimeSpan.FromMilliseconds(10000), onSuccess, onException);
                        var localSent = Interlocked.Increment(ref sent);
                        while (localSent - Interlocked.Read(ref received) > context.Client.Options.ReadWindow/clientsCnt)
                        {
                            Thread.Sleep(1);
                        }
                    }
                }) { IsBackground = true });
            }

            var sw = Stopwatch.StartNew();
            sw2.Start();
            threads.ForEach(thread => thread.Start());
            doneEvent.Wait();
            sw.Stop();

            var reqPerSec = (all + 0.0)/sw.ElapsedMilliseconds*1000;
            context.Log.Info("Completed. READS succ: {0}, fail: {1}.", Interlocked.Read(ref succ), Interlocked.Read(ref fail));
            context.Log.Info("{0} requests completed in {1}ms ({2:0.00} reqs per sec).", all, sw.ElapsedMilliseconds, reqPerSec);
            PerfUtils.LogData(Keyword,
                              PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
                                            PerfUtils.Col("requestsCnt", requestsCnt),
                                            PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
                              PerfUtils.Row(PerfUtils.Col("readsCnt", all)));
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt), (int) reqPerSec);

            if (Interlocked.Read(ref succ) == requestsCnt)
                context.Success();
            else
                context.Fail();
        }
    }
}