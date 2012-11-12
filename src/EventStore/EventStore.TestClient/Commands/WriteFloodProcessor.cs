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
using System.Linq;
using System.Text;
using System.Threading;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class WriteFloodProcessor : ICmdProcessor
    {
        public string Usage { get { return "WRFL [<clients> <requests> [<streams-cnt>]]"; } }
        public string Keyword { get { return "WRFL"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            long requestsCnt = 5000;
            int streamsCnt = 1000;
            if (args.Length > 0)
            {
                if (args.Length != 2 && args.Length != 3)
                    return false;

                try
                {
                    clientsCnt = int.Parse(args[0]);
                    requestsCnt = long.Parse(args[1]);
                    if (args.Length >= 3)
                        streamsCnt = int.Parse(args[2]);
                }
                catch
                {
                    return false;
                }
            }

            WriteFlood(context, clientsCnt, requestsCnt, streamsCnt);
            return true;
        }

        private void WriteFlood(CommandProcessorContext context, int clientsCnt, long requestsCnt, int streamsCnt)
        {
            context.IsAsync();

            var doneEvent = new AutoResetEvent(false);
            var clients = new List<TcpTypedConnection<byte[]>>();
            var threads = new List<Thread>();

            long succ = 0;
            long fail = 0;
            long prepTimeout = 0;
            long commitTimeout = 0;
            long forwardTimeout = 0;
            long wrongExpVersion = 0;
            long streamDeleted = 0;
            long all = 0;

            var streams = Enumerable.Range(0, streamsCnt).Select(x => Guid.NewGuid().ToString()).ToArray();
            var sw2 = new Stopwatch();

            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);

                int sent = 0;
                int received = 0;
                var rnd = new Random();

                var client = context.Client.CreateTcpConnection(
                    context,
                    (conn, pkg) =>
                    {
                        if (pkg.Command != TcpCommand.WriteEventsCompleted)
                        {
                            context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                            return;
                        }

                        var dto = pkg.Data.Deserialize<TcpClientMessageDto.WriteEventsCompleted>();
                        switch((OperationErrorCode)dto.ErrorCode)
                        {
                            case OperationErrorCode.Success:
                                Interlocked.Increment(ref succ);
                                break;
                            case OperationErrorCode.PrepareTimeout:
                                Interlocked.Increment(ref prepTimeout);
                                break;
                            case OperationErrorCode.CommitTimeout:
                                Interlocked.Increment(ref commitTimeout);
                                break;
                            case OperationErrorCode.ForwardTimeout:
                                Interlocked.Increment(ref forwardTimeout);
                                break;
                            case OperationErrorCode.WrongExpectedVersion:
                                Interlocked.Increment(ref wrongExpVersion);
                                break;
                            case OperationErrorCode.StreamDeleted:
                                Interlocked.Increment(ref streamDeleted);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        if (dto.ErrorCode != (int)OperationErrorCode.Success)
                            Interlocked.Increment(ref fail);

                        Interlocked.Increment(ref received);

                        var localAll = Interlocked.Increment(ref all);
                        if (localAll % 1000 == 0) Console.Write(".");
                        if (localAll % 100000 == 0)
                        {
                            var elapsed = sw2.Elapsed;
                            sw2.Restart();
                            context.Log.Trace("\nDONE TOTAL {0} WRITES IN {1} ({2:0.0}/s).",
                                              localAll,
                                              elapsed,
                                              1000.0*100000/elapsed.TotalMilliseconds);
                        }
                        if (localAll == requestsCnt)
                            doneEvent.Set();
                    },
                    connectionClosed: (conn, err) =>
                    {
                        if (all < requestsCnt)
                            context.Fail(null, "Socket was closed, but not all requests were completed.");
                        else
                            context.Success();
                    });

                clients.Add(client);

                threads.Add(new Thread(() =>
                {
                    for (int j = 0; j < count; ++j)
                    {
                        var write = new TcpClientMessageDto.WriteEvents(
                            streams[rnd.Next(streamsCnt)],
                            ExpectedVersion.Any,
                            new[]
                                {
                                    new TcpClientMessageDto.ClientEvent(Guid.NewGuid().ToByteArray(),
                                                                        "TakeSomeSpaceEvent",
                                                                        Encoding.UTF8.GetBytes("DATA" + new string('*', 256)),
                                                                        Encoding.UTF8.GetBytes("METADATA" + new string('$', 100)))
                                },
                            true);
                        var package = new TcpPackage(TcpCommand.WriteEvents, Guid.NewGuid(), write.Serialize());
                        client.EnqueueSend(package.AsByteArray());

                        Interlocked.Increment(ref sent);
                        while (sent - received > context.Client.Options.WriteWindow)
                            Thread.Sleep(1);
                    }
                }));
            }

            var sw = Stopwatch.StartNew();
            sw2.Start();
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

            context.Log.Info("Completed. Successes: {0}, failures: {1} (WRONG VERSION: {2}, P: {3}, C: {4}, F: {5}, D: {6})",
                             succ,
                             fail,
                             wrongExpVersion,
                             prepTimeout,
                             commitTimeout,
                             forwardTimeout,
                             streamDeleted);

            var reqPerSec = (all + 0.0) / sw.ElapsedMilliseconds * 1000;
            context.Log.Info("{0} requests completed in {1}ms ({2:0.00} reqs per sec).",
                             all,
                             sw.ElapsedMilliseconds,
                             reqPerSec);

            PerfUtils.LogData(
                Keyword,
                PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
                              PerfUtils.Col("requestsCnt", requestsCnt),
                              PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
                PerfUtils.Row(PerfUtils.Col("successes", succ), PerfUtils.Col("failures", fail)));

            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt),
                                           (int) reqPerSec);

            PerfUtils.LogTeamCityGraphData(
                string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt),
                100*fail/(fail + succ));

            if (succ < fail)
                context.Fail(reason: "Number of failures is greater than number of successes");
            else
                context.Success();
        }
    }
}