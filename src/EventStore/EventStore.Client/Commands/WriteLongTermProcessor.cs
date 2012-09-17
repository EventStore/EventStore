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
using System.Text;
using System.Threading;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class WriteLongTermProcessor : ICmdProcessor
    {
        public string Usage
        {
            get { return "WRLT <clients> <min req. per second> <max req. per second> <run for n minutes> [<event-stream>]"; }
        }

        public string Keyword { get { return "WRLT"; } }

        private readonly object _randomLockRoot = new object();
        private readonly Random _random = new Random();

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            int minPerSecond = 1;
            int maxPerSecond = 2;
            int runTimeMinutes = 1;
            string eventStreamId = null;

            if (args.Length > 0)
            {
                if (args.Length != 4 && args.Length != 5)
                    return false;

                try
                {
                    clientsCnt = int.Parse(args[0]);
                    minPerSecond = int.Parse(args[1]);
                    maxPerSecond = int.Parse(args[2]);
                    runTimeMinutes = int.Parse(args[3]);
                    if (args.Length == 5)
                        eventStreamId = args[4];
                }
                catch
                {
                    return false;
                }
            }

            Flood(context, eventStreamId, clientsCnt, minPerSecond, maxPerSecond, runTimeMinutes);
            return true;
        }

        private void Flood(CommandProcessorContext context, 
                           string eventStreamId, 
                           int clientsCnt, 
                           int minPerSecond, 
                           int maxPerSecond, 
                           int runTimeMinutes)
        {
            context.IsAsync();

            var clients = new List<TcpTypedConnection<byte[]>>();
            var threads = new List<Thread>();
            var doneEvent = new ManualResetEvent(false);
            var done = false;

            var succ = 0;
            var fail = 0;

            var requestsCnt = 0;

            var watchLockRoot = new object();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < clientsCnt; i++)
            {
                int sent = 0;
                int received = 0;

                var esId = eventStreamId ?? "Stream-" + Thread.CurrentThread.ManagedThreadId % 3;

                var client = context.Client.CreateTcpConnection(
                    context,
                    (conn, pkg) =>
                    {
                        if (pkg.Command != TcpCommand.WriteEventsCompleted)
                        {
                            context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                            return;
                        }

                        var dto = pkg.Data.Deserialize<ClientMessageDto.WriteEventsCompleted>();
                        if (dto.ErrorCode == (int)OperationErrorCode.Success)
                        {
                            var succDone = Interlocked.Increment(ref succ);
                            if (succDone%maxPerSecond == 0)
                                Console.Write(".");

                            Interlocked.Increment(ref requestsCnt);
                        }
                        else
                            Interlocked.Increment(ref fail);

                        Interlocked.Increment(ref received);
                    },
                    connectionClosed: (conn, err) =>
                    {
                        if (!done)
                            context.Fail(reason: "Socket was closed, but not all requests were completed.");
                        else
                            context.Success();
                    });
                clients.Add(client);

                threads.Add(new Thread(() =>
                {
                    var sentCount = 0;
                    var sleepTime = 0;

                    var dataSizeCoefficient = 1;
                    var currentMinute = -1;

                    while(true)
                    {
                        TimeSpan elapsed;
                        lock (watchLockRoot)
                            elapsed = sw.Elapsed;

                        if (elapsed.TotalMinutes > runTimeMinutes)
                        {
                            done = true;
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

                        var dataSize = dataSizeCoefficient * 8;
                        var write = new ClientMessageDto.WriteEvents(
                            Guid.Empty,
                            esId,
                            ExpectedVersion.Any,
                            new[] { 
                                new ClientMessageDto.Event(
                                    Guid.NewGuid() ,
                                    "TakeSomeSpaceEvent",
                                    Encoding.UTF8.GetBytes("DATA" + dataSize.ToString(" 00000 ") + new string('*', dataSize)),
                                    Encoding.UTF8.GetBytes("METADATA" + new string('$', 100)))
                            });
                        var package = new TcpPackage(TcpCommand.WriteEvents, write.Serialize());
                        client.EnqueueSend(package.AsByteArray());

                        Interlocked.Increment(ref sent);

                        Thread.Sleep(sleepTime);
                        sentCount -= 1;

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

            doneEvent.WaitOne();
            sw.Stop();

            foreach (var client in clients)
            {
                client.Close();
            }

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