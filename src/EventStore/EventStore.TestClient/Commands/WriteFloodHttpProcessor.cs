/*
GFY NOT USED
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.ClientAPI.Common;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;
using EventStore.Transport.Http.Codecs;
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
            long requestsCnt = 5000;
            string eventStreamId = null;
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

            WriteFlood(context, eventStreamId, clientsCnt, requestsCnt);
            return true;
        }

        private void WriteFlood(CommandProcessorContext context, string eventStreamId, int clientsCnt, long requestsCnt)
        {
            context.IsAsync();

            var threads = new List<Thread>();
            var doneEvent = new ManualResetEventSlim(false);
            long all = 0;
            long succ = 0;
            long fail = 0;
            var sw = new Stopwatch();
            var sw2 = new Stopwatch();
            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
                long sent = 0;
                long received = 0;
                threads.Add(new Thread(() =>
                {
                    var esId = eventStreamId ?? ("es" + Guid.NewGuid());
                    var client = new HttpAsyncClient();

                    Action onReceived = () =>
                    {
                        Interlocked.Increment(ref received);
                        var localAll = Interlocked.Increment(ref all);
                        if (localAll % 10000 == 0)
                        {
                            var elapsed = sw2.Elapsed;
                            sw2.Restart();
                            context.Log.Trace("\nDONE TOTAL {0} WRITES IN {1} ({2:0.0}/s).", localAll, elapsed, 1000.0 * 10000 / elapsed.TotalMilliseconds);
                        }
                        if (localAll == requestsCnt)
                            doneEvent.Set();
                    };
                    Action<HttpResponse> onSuccess = response =>
                    {
                        if (response.HttpStatusCode == HttpStatusCode.Created)
                        {
                            if (Interlocked.Increment(ref succ) % 100 == 0) Console.Write('.');
                        }
                        else
                        {
                            var localFail = Interlocked.Increment(ref fail);
                            if (localFail % 100 == 0) Console.Write('#');
                            if (localFail % 10 == 0)
                                context.Log.Info("ANOTHER 10th WRITE FAILED. [{0}] - [{1}]", response.HttpStatusCode, response.StatusDescription);
                        }
                        onReceived();
                    };
                    Action<Exception> onException = exc =>
                    {
                        context.Log.ErrorException(exc, "Error during POST.");
                        if (Interlocked.Increment(ref fail) % 100 == 0) Console.Write('#');
                        onReceived();
                    };

                    for (int j = 0; j < count; ++j)
                    {
                        var url = context.Client.HttpEndpoint.ToHttpUrl("/streams/{0}", esId);
                        var write = new[] { new HttpClientMessageDto.ClientEventText(Guid.NewGuid(), 
                                                                                     "type",
                                                                                     "DATA" + new string('*', 256),
                                                                                     "METADATA" + new string('$', 100)) };

                        var requestString = Codec.Json.To(write);
                        client.Post(
                            url, 
                            requestString,
                            Codec.Json.ContentType,
                            new Dictionary<string, string> { },
                            TimeSpan.FromMilliseconds(10000),
                            onSuccess, onException);
                        
                        var localSent = Interlocked.Increment(ref sent);
                        while (localSent - Interlocked.Read(ref received) > context.Client.Options.WriteWindow/clientsCnt)
                        {
                            Thread.Sleep(1);
                        }
                    }
                }) { IsBackground = true });
            }

            sw.Start();
            sw2.Start();
            threads.ForEach(thread => thread.Start());
            doneEvent.Wait();
            sw.Stop();

            var reqPerSec = (all + 0.0) / sw.ElapsedMilliseconds * 1000;
            context.Log.Info("Completed. Successes: {0}, failures: {1}", Interlocked.Read(ref succ), Interlocked.Read(ref fail));
            context.Log.Info("{0} requests completed in {1}ms ({2:0.00} reqs per sec).", all, sw.ElapsedMilliseconds, reqPerSec);
            PerfUtils.LogData(Keyword,
                              PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
                                            PerfUtils.Col("requestsCnt", requestsCnt),
                                            PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
                              PerfUtils.Row(PerfUtils.Col("successes", succ), PerfUtils.Col("failures", fail)));
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt), (int)reqPerSec);
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt), (int)(100.0*fail/(fail + succ)));

            if (Interlocked.Read(ref succ) != requestsCnt)
                context.Fail(reason: "There were errors or not all requests completed.");
            else
                context.Success();
        }
    }
}
*/