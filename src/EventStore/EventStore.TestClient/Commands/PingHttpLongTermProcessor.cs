/*
GFY NOT USED
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;

namespace EventStore.TestClient.Commands
{
    public class PingHttpLongTermProcessor : ICmdProcessor
    {
        public string Usage
        {
            get { return "PINGLTH <clients> <min req. per second> <max req. per second> <run for n minutes>"; }
        }

        public string Keyword { get { return "PINGLTH"; } }

        private readonly object _randomLockRoot = new object();
        private readonly Random _random = new Random();

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            int minPerSecond = 1;
            int maxPerSecond = 2;
            int runTimeMinutes = 1;

            if (args.Length > 0)
            {
                if (args.Length != 4)
                    return false;

                try
                {
                    clientsCnt = int.Parse(args[0]);
                    minPerSecond = int.Parse(args[1]);
                    maxPerSecond = int.Parse(args[2]);
                    runTimeMinutes = int.Parse(args[3]);
                }
                catch
                {
                    return false;
                }
            }

            Flood(context, clientsCnt, minPerSecond, maxPerSecond, runTimeMinutes);
            return true;
        }

        private void Flood(CommandProcessorContext context, 
                           int clientsCnt, 
                           int minPerSecond, 
                           int maxPerSecond, 
                           int runTimeMinutes)
        {
            context.IsAsync();

            var threads = new List<Thread>();
            var autoResetEvent = new AutoResetEvent(false);

            var succ = 0;
            var fail = 0;

            var requestsCnt = 0;

            int sent = 0;
            int received = 0;

            var watchLockRoot = new object();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < clientsCnt; i++)
            {
                threads.Add(new Thread(() =>
                {
                    var client = new HttpAsyncClient();

                    Action<HttpResponse> succHandler = response =>
                    {
                        var succDone = Interlocked.Increment(ref succ);
                        if (succDone % maxPerSecond == 0)
                            Console.Write(".");

                        Interlocked.Increment(ref requestsCnt);
                        Interlocked.Increment(ref received);
                    };

                    var sentCount = 0;
                    var sleepTime = 0;

                    var currentMinute = -1;

                    while (true)
                    {
                        TimeSpan elapsed;
                        lock (watchLockRoot)
                            elapsed = sw.Elapsed;

                        if (elapsed.TotalMinutes > runTimeMinutes)
                        {
                            autoResetEvent.Set();
                            break;
                        }

                        if (sentCount == 0)
                        {
                            int elapsedMinutesInt = (int)elapsed.TotalMinutes;

                            lock (_randomLockRoot)
                            {
                                sentCount = minPerSecond == maxPerSecond
                                                ? maxPerSecond
                                                : _random.Next(minPerSecond, maxPerSecond);
                            }

                            if (currentMinute != elapsedMinutesInt)
                            {
                                currentMinute = elapsedMinutesInt;
                                context.Log.Info(Environment.NewLine + "Elapsed {0} of {1} minutes, sent {2}",
                                                 elapsedMinutesInt,
                                                 runTimeMinutes,
                                                 sent);
                            }

                            sleepTime = 1000 / sentCount;
                        }

                        var url = context.Client.HttpEndpoint.ToHttpUrl("/ping");

                        client.Get(url, 
                                   null,
                                   TimeSpan.FromMilliseconds(10000),
                                   succHandler,
                                   exc => context.Log.ErrorException(exc, "Error during GET."));

                        Interlocked.Increment(ref sent);

                        Thread.Sleep(sleepTime);
                        sentCount -= 1;

                        while (sent - received > context.Client.Options.PingWindow/clientsCnt)
                        {
                            Thread.Sleep(1);
                        }
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
                                           (int) reqPerSec);

            PerfUtils.LogTeamCityGraphData(
                string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt),
                (int) ((fail/(succ + 0.0))*100));

            context.Success();
        }
    }
}
*/