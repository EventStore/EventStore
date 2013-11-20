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
    public class PingFloodHttpProcessor : ICmdProcessor
    {
        public string Usage { get { return string.Format("{0} [<clients> <messages>]", Keyword); } }
        public string Keyword { get { return "PINGFLH"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            long requestsCnt = 100 * 1000;
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

            PingFlood(context, clientsCnt, requestsCnt);
            return true;
        }

        private void PingFlood(CommandProcessorContext context, int clientsCnt, long requestsCnt)
        {
            context.IsAsync();

            var threads = new List<Thread>();
            var doneEvent = new ManualResetEventSlim(false);
            long all = 0;
            long succ = 0;
            long fail = 0;
            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
                long received = 0;
                long sent = 0;
                threads.Add(new Thread(() =>
                {
                    var client = new HttpAsyncClient();
                    var url = context.Client.HttpEndpoint.ToHttpUrl("/ping");

                    Action<HttpResponse> onSuccess = response =>
                    {
                        Interlocked.Increment(ref received);
                        if (Interlocked.Increment(ref succ) % 1000 == 0) Console.Write('.');
                        if (Interlocked.Increment(ref all) == requestsCnt)
                            doneEvent.Set();
                    };

                    Action<Exception> onException = e =>
                    {
                        context.Log.ErrorException(e, "Error during GET");
                        Interlocked.Increment(ref received);
                        if (Interlocked.Increment(ref fail) % 1000 == 0) Console.Write('#');
                        if (Interlocked.Increment(ref all) == requestsCnt)
                            doneEvent.Set();
                    };

                    for (int j = 0; j < count; ++j)
                    {
                        client.Get(url, TimeSpan.FromMilliseconds(10000), onSuccess, onException);
                        var localSent = Interlocked.Increment(ref sent);
                        while (localSent - Interlocked.Read(ref received) > context.Client.Options.PingWindow/clientsCnt)
                        {
                            Thread.Sleep(1);
                        }
                    }
                }) { IsBackground = true });
            }

            var sw = Stopwatch.StartNew();
            threads.ForEach(thread => thread.Start());
            doneEvent.Wait();
            sw.Stop();

            var reqPerSec = (all + 0.0) / sw.ElapsedMilliseconds * 1000;
            context.Log.Info("{0} requests completed in {1}ms ({2:0.00} reqs per sec).", all, sw.ElapsedMilliseconds, reqPerSec);
            PerfUtils.LogData(Keyword,
                              PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
                                            PerfUtils.Col("requestsCnt", requestsCnt),
                                            PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)));
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt), (int)reqPerSec);

            if (Interlocked.Read(ref succ) == requestsCnt)
                context.Success();
            else
                context.Fail();
        }
    }
}