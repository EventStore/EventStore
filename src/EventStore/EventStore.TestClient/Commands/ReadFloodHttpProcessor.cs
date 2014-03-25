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