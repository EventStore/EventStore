/*
GFY NOT USED
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
using EventStore.Transport.Http.Codecs;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;

namespace EventStore.TestClient.Commands
{
    public class WriteLongTermHttpProcessor : ICmdProcessor
    {
        public string Usage
        {
            get { return "WRLTH <clients> <min req. per second> <max req. per second> <run for n minutes> [<data-size> [<event-stream>]]"; }
        }

        public string Keyword { get { return "WRLTH"; } }

        private readonly object _randomLockRoot = new object();
        private readonly Random _random = new Random();

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            int minPerSecond = 1;
            int maxPerSecond = 2;
            int runTimeMinutes = 1;
            int dataSize = 8;
            string eventStreamId = null;

            if (args.Length > 0)
            {
                if (args.Length != 4 && args.Length != 5 && args.Length != 6)
                    return false;

                try
                {
                    clientsCnt = int.Parse(args[0]);
                    minPerSecond = int.Parse(args[1]);
                    maxPerSecond = int.Parse(args[2]);
                    runTimeMinutes = int.Parse(args[3]);
                    if (args.Length >= 5)
                        dataSize = int.Parse(args[4]);
                    if (args.Length >= 6)
                        eventStreamId = args[5];
                }
                catch
                {
                    return false;
                }
            }

            Flood(context, eventStreamId, clientsCnt, minPerSecond, maxPerSecond, runTimeMinutes, dataSize);
            return true;
        }

        private void Flood(CommandProcessorContext context, 
                           string eventStreamId, 
                           int clientsCnt, 
                           int minPerSecond, 
                           int maxPerSecond, 
                           int runTimeMinutes,
                           int dataSize)
        {
            context.IsAsync();

            var threads = new List<Thread>();
            var doneEvent = new ManualResetEvent(false);

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
                    var esId = eventStreamId ?? "Stream-" + Thread.CurrentThread.ManagedThreadId % 3;

                    var client = new HttpAsyncClient();

                    Action<HttpResponse> succHandler = response =>
                    {
                        if (response.HttpStatusCode == HttpStatusCode.Created)
                        {
                            var succDone = Interlocked.Increment(ref succ);
                            if (succDone % maxPerSecond == 0)
                                Console.Write(".");
                        }
                        else
                        {
                            if (Interlocked.Increment(ref fail) % 10 == 0)
                            {
                                context.Log.Info("ANOTHER 10th WRITE FAILED. [{0}] - [{1}]", response.HttpStatusCode, response.StatusDescription);
                            }
                        }

                        Interlocked.Increment(ref requestsCnt);
                        Interlocked.Increment(ref received);
                    };

                    var sentCount = 0;
                    var sleepTime = 0;

                    var dataSizeCoefficient = 1;
                    var currentMinute = -1;

                    while (true)
                    {
                        TimeSpan elapsed;
                        lock (watchLockRoot)
                            elapsed = sw.Elapsed;

                        if (elapsed.TotalMinutes > runTimeMinutes)
                        {
                            doneEvent.Set();
                            break;
                        }

                        if (sentCount == 0)
                        {
                            int elapsedMinutesInt = (int)elapsed.TotalMinutes;

                            lock (_randomLockRoot)
                            {
                                sentCount = minPerSecond == maxPerSecond
                                            ? maxPerSecond : _random.Next(minPerSecond, maxPerSecond);
                                dataSizeCoefficient = _random.Next(8, 256);
                            }

                            if (currentMinute != elapsedMinutesInt)
                            {
                                currentMinute = elapsedMinutesInt;
                                context.Log.Info("\nElapsed {0} of {1} minutes, sent {2}; next block coef. {3}",
                                                 elapsedMinutesInt,
                                                 runTimeMinutes,
                                                 sent,
                                                 dataSizeCoefficient);
                            }

                            sleepTime = 1000 / sentCount;
                        }

                        var url = context.Client.HttpEndpoint.ToHttpUrl("/streams/{0}", esId);

                        var dataResultingSize = dataSizeCoefficient * dataSize;
                        var write = new[] { new HttpClientMessageDto.ClientEventText(Guid.NewGuid(),
                                                                                     "type",
                                                                                     "DATA" + dataResultingSize.ToString(" 00000 ") + new string('*', dataResultingSize),
                                                                                     "METADATA" + new string('$', 100))};
                        var request = Codec.Xml.To(write);
                        client.Post(url, 
                                    request, 
                                    Codec.Xml.ContentType,
                                    TimeSpan.FromMilliseconds(10000),
                                    succHandler,
                                    exc =>
                                        {
                                            Interlocked.Increment(ref fail);
                                            Interlocked.Increment(ref requestsCnt);
                                            context.Log.ErrorException(exc, "Error during POST.");
                                        });

                        Interlocked.Increment(ref sent);

                        Thread.Sleep(sleepTime);
                        sentCount -= 1;

                        while (sent - received > context.Client.Options.WriteWindow/clientsCnt)
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

            doneEvent.WaitOne();

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

            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-{3}-{4}-reqPerSec", 
                                                   Keyword, 
                                                   clientsCnt, 
                                                   minPerSecond,
                                                   maxPerSecond,
                                                   runTimeMinutes),
                        (int)reqPerSec);

            PerfUtils.LogTeamCityGraphData(
                string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt),
                100 * fail / (fail + succ));

            context.Success();
        }
    }
}
*/