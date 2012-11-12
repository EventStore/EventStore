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
using System.Net;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;

namespace EventStore.TestClient.Commands
{
    internal class WriteFloodHttpProcessor : ICmdProcessor
    {
        public string Usage { get { return "WRFLH [<clients> <requests> [<event-stream>]]"; } }
        public string Keyword { get { return "WRFLH"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            int requestsCnt = 5000;
            string eventStreamId = null;
            if (args.Length > 0)
            {
                if (args.Length != 2 && args.Length != 3)
                    return false;

                try
                {
                    clientsCnt = int.Parse(args[0]);
                    requestsCnt = int.Parse(args[1]);
                    if (args.Length == 3)
                        eventStreamId = args[2];
                }
                catch
                {
                    return false;
                }
            }

            WriteFlood(context, eventStreamId, clientsCnt, requestsCnt);
            return true;
        }

        private void WriteFlood(CommandProcessorContext context, string eventStreamId, int clientsCnt, int requestsCnt)
        {
            context.IsAsync();

            var threads = new List<Thread>();
            var autoResetEvent = new AutoResetEvent(false);

            var succ = 0;
            var fail = 0;
            var all = 0;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);

                int sent = 0;
                int received = 0;

                threads.Add(new Thread(() =>
                {
                    var esId = eventStreamId ?? "es" + Guid.NewGuid();

                    var client = new HttpAsyncClient();

                    Action<HttpResponse> succHandler = response =>
                    {
                        if (response.HttpStatusCode == HttpStatusCode.Created)
                        {
                            if (Interlocked.Increment(ref succ) % 100 == 0)
                                Console.Write(".");
                        }
                        else
                        {
                            if (Interlocked.Increment(ref fail) % 10 == 0)
                            {
                                context.Log.Info("ANOTHER 10th WRITE FAILED. [{0}] - [{1}]", response.HttpStatusCode, response.StatusDescription);
                            }
                        }

                        Interlocked.Increment(ref received);
                        if (Interlocked.Increment(ref all) == requestsCnt)
                            autoResetEvent.Set();
                    };

                    for (int j = 0; j < count; ++j)
                    {
                        var url = context.Client.HttpEndpoint.ToHttpUrl("/streams/{0}", esId);
                        var write = new HttpClientMessageDto.WriteEventsText(
                            ExpectedVersion.Any,
                            new[] { 
                                new HttpClientMessageDto.ClientEventText(Guid.NewGuid(), 
                                                               "type",
                                                               "DATA" + new string('*', 256),
                                                               "METADATA" + new string('$', 100))
                            });
                        var requestString = Codec.Xml.To(write);
                        client.Post(url, requestString, Codec.Xml.ContentType, succHandler, exc => 
                        {
                            context.Log.ErrorException(exc, "Error during POST.");
                            Interlocked.Increment(ref fail);
                            if (Interlocked.Increment(ref all) == requestsCnt)
                                autoResetEvent.Set();
                        });
                        
                        Interlocked.Increment(ref sent);

                        while (sent - received > context.Client.Options.WriteWindow)
                            Thread.Sleep(1);
                    }
                }));
            }

            foreach (var thread in threads)
            {
                thread.IsBackground = true;
                thread.Start();
            }

            autoResetEvent.WaitOne();
            sw.Stop();

            context.Log.Info("Completed. Successes: {0}, failures: {1}", succ, fail);
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
                        PerfUtils.Row(PerfUtils.Col("successes", succ), PerfUtils.Col("failures", fail))
            );

            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt),
                        (int)reqPerSec);

            PerfUtils.LogTeamCityGraphData(
                string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt),
                100*fail/(fail + succ));

            context.Success();
        }
    }
}