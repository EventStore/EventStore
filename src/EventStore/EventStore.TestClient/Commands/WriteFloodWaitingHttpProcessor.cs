using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;
using EventStore.Transport.Http.Codecs;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;

namespace EventStore.TestClient.Commands
{
    internal class WriteFloodWaitingHttpProcessor : ICmdProcessor
    {
        public string Usage { get { return "WRFLWH [<clients> <requests>]"; } }
        public string Keyword { get { return "WRFLWH"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            int requestsCnt = 5000;
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

            WriteFlood(context, clientsCnt, requestsCnt);
            return true;
        }

        private void WriteFlood(CommandProcessorContext context, int clientsCnt, int requestsCnt)
        {
            context.IsAsync();

            var threads = new List<Thread>();
            var doneEvent = new ManualResetEventSlim(false);
            var succ = 0;
            var fail = 0;
            var all = 0;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
                threads.Add(new Thread(() =>
                {
                    var autoEvent = new AutoResetEvent(false);
                    var eventStreamId = "es" + Guid.NewGuid();
                    var client = new HttpAsyncClient();
                    var url = context.Client.HttpEndpoint.ToHttpUrl("/streams/{0}", eventStreamId);
                    Action<HttpResponse> succHandler = response =>
                    {
                        if (response.HttpStatusCode == HttpStatusCode.Created)
                        {
                            if (Interlocked.Increment(ref succ)%1000 == 0) Console.Write(".");
                        }
                        else
                        {
                            if (Interlocked.Increment(ref fail)%10 == 0)
                                context.Log.Info("ANOTHER 10th WRITE FAILED. [{0}] - [{1}]", response.HttpStatusCode, response.StatusDescription);
                        }
                        if (Interlocked.Increment(ref all) == requestsCnt)
                            doneEvent.Set();
                        autoEvent.Set();
                    };

                    for (int j = 0; j < count; ++j)
                    {
                        var write = new[] { new HttpClientMessageDto.ClientEventText(Guid.NewGuid(), 
                                                                                     "type",
                                                                                     "DATA" + new string('*', 256),
                                                                                     "METADATA" + new string('$', 100))};
                        var request = Codec.Xml.To(write);
                        client.Post(url, 
                                    request,
                                    Codec.Xml.ContentType,
                                    TimeSpan.FromMilliseconds(10000),
                                    succHandler, 
                                    exc => 
                                    {
                                        context.Log.ErrorException(exc, "Error during POST.");
                                        Interlocked.Increment(ref fail);
                                        if (Interlocked.Increment(ref all) == requestsCnt)
                                            doneEvent.Set();
                                        autoEvent.Set();
                                    });
                        autoEvent.WaitOne();
                    }
                }) { IsBackground = true });
            }

            threads.ForEach(thread => thread.Start());
            doneEvent.Wait();
            sw.Stop();

            var reqPerSec = (all + 0.0)/sw.ElapsedMilliseconds*1000;
            context.Log.Info("Completed. Successes: {0}, failures: {1}", succ, fail);
            context.Log.Info("{0} requests completed in {1}ms ({2:0.00} reqs per sec).", all, sw.ElapsedMilliseconds, reqPerSec);
            PerfUtils.LogData(Keyword,
                              PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
                                            PerfUtils.Col("requestsCnt", requestsCnt),
                                            PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
                              PerfUtils.Row(PerfUtils.Col("successes", succ), PerfUtils.Col("failures", fail)));
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt), (int) reqPerSec);
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt), (int)(100.0*fail/(fail + succ)));
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword), (int)Math.Round(sw.Elapsed.TotalMilliseconds / requestsCnt));

            if (succ != requestsCnt)
                context.Fail(reason: "There were errors or not all requests completed.");
            else
                context.Success();
        }
    }
}