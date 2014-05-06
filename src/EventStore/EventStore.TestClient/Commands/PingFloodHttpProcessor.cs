/*
GFY NOT USED
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
*/