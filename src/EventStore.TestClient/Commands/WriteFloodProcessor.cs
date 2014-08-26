using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class WriteFloodProcessor : ICmdProcessor
    {
        public string Usage { get { return "WRFL [<clients> <requests> [<streams-cnt> [<size>]]]"; } }
        public string Keyword { get { return "WRFL"; } }

        private RequestMonitor _monitor = new RequestMonitor();

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            int clientsCnt = 1;
            long requestsCnt = 5000;
            int streamsCnt = 1000;
            int size = 256;
            if (args.Length > 0)
            {
                if (args.Length < 2 || args.Length > 4)
                    return false;

                try
                {
                    clientsCnt = int.Parse(args[0]);
                    requestsCnt = long.Parse(args[1]);
                    if (args.Length >= 3)
                        streamsCnt = int.Parse(args[2]);
                    if (args.Length >= 4)
                        size = int.Parse(args[3]);
                }
                catch
                {
                    return false;
                }
            }

            WriteFlood(context, clientsCnt, requestsCnt, streamsCnt, size);
            return true;
        }

        private void WriteFlood(CommandProcessorContext context, int clientsCnt, long requestsCnt, int streamsCnt, int size)
        {
            context.IsAsync();

            var doneEvent = new ManualResetEventSlim(false);
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
            //var streams = Enumerable.Range(0, streamsCnt).Select(x => string.Format("stream-{0}", x)).ToArray();
            var sw2 = new Stopwatch();
            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
                long sent = 0;
                long received = 0;
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
                        _monitor.EndOperation(pkg.CorrelationId);
                        switch(dto.Result)
                        {
                            case TcpClientMessageDto.OperationResult.Success:
                                if (Interlocked.Increment(ref succ) % 1000 == 0) Console.Write('.');
                                break;
                            case TcpClientMessageDto.OperationResult.PrepareTimeout:
                                Interlocked.Increment(ref prepTimeout);
                                break;
                            case TcpClientMessageDto.OperationResult.CommitTimeout:
                                Interlocked.Increment(ref commitTimeout);
                                break;
                            case TcpClientMessageDto.OperationResult.ForwardTimeout:
                                Interlocked.Increment(ref forwardTimeout);
                                break;
                            case TcpClientMessageDto.OperationResult.WrongExpectedVersion:
                                Interlocked.Increment(ref wrongExpVersion);
                                break;
                            case TcpClientMessageDto.OperationResult.StreamDeleted:
                                Interlocked.Increment(ref streamDeleted);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        if (dto.Result != TcpClientMessageDto.OperationResult.Success)
                            if (Interlocked.Increment(ref fail)%1000 == 0) 
                                Console.Write('#');
                        Interlocked.Increment(ref received);
                        var localAll = Interlocked.Increment(ref all);
                        if (localAll % 100000 == 0)
                        {
                            var elapsed = sw2.Elapsed;
                            sw2.Restart();
                            context.Log.Trace("\nDONE TOTAL {0} WRITES IN {1} ({2:0.0}/s) [S:{3}, F:{4} (WEV:{5}, P:{6}, C:{7}, F:{8}, D:{9})].",
                                              localAll, elapsed, 1000.0*100000/elapsed.TotalMilliseconds,
                                              succ, fail,
                                              wrongExpVersion, prepTimeout, commitTimeout, forwardTimeout, streamDeleted);
                        }
                        if (localAll == requestsCnt)
                        {
                            context.Success();
                            doneEvent.Set();
                        }
                    },
                    connectionClosed: (conn, err) => context.Fail(reason: "Connection was closed prematurely."));
                clients.Add(client);

                threads.Add(new Thread(() =>
                {
                    for (int j = 0; j < count; ++j)
                    {
                        var corrid = Guid.NewGuid();
                        var write = new TcpClientMessageDto.WriteEvents(
                            streams[rnd.Next(streamsCnt)],
                            ExpectedVersion.Any,
                            new[]
                            {
                                new TcpClientMessageDto.NewEvent(Guid.NewGuid().ToByteArray(),
                                                                 "TakeSomeSpaceEvent",
                                                                 1,0,
                                                                 Common.Utils.Helper.UTF8NoBom.GetBytes("{ \"DATA\" : \"" + new string('*', size) + "\"}"),
                                                                 Common.Utils.Helper.UTF8NoBom.GetBytes("{ \"METADATA\" : \"" + new string('$', 100) + "\"}"))
                            },
                            false);
                        var package = new TcpPackage(TcpCommand.WriteEvents, corrid, write.Serialize());
                        _monitor.StartOperation(corrid);
                        client.EnqueueSend(package.AsByteArray());

                        var localSent = Interlocked.Increment(ref sent);
                        while (localSent - Interlocked.Read(ref received) > context.Client.Options.WriteWindow/clientsCnt)
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
            clients.ForEach(client => client.Close());

            context.Log.Info("Completed. Successes: {0}, failures: {1} (WRONG VERSION: {2}, P: {3}, C: {4}, F: {5}, D: {6})",
                             succ, fail,
                             wrongExpVersion, prepTimeout, commitTimeout, forwardTimeout, streamDeleted);

            var reqPerSec = (all + 0.0) / sw.ElapsedMilliseconds * 1000;
            context.Log.Info("{0} requests completed in {1}ms ({2:0.00} reqs per sec).", all, sw.ElapsedMilliseconds, reqPerSec);

            PerfUtils.LogData(
                Keyword,
                PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
                              PerfUtils.Col("requestsCnt", requestsCnt),
                              PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
                PerfUtils.Row(PerfUtils.Col("successes", succ), PerfUtils.Col("failures", fail)));

            var failuresRate = (int) (100 * fail / (fail + succ));
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt), (int)reqPerSec);
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt), failuresRate);
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-c{1}-r{2}-st{3}-s{4}-reqPerSec", Keyword, clientsCnt, requestsCnt, streamsCnt, size), (int)reqPerSec);
            PerfUtils.LogTeamCityGraphData(string.Format("{0}-c{1}-r{2}-st{3}-s{4}-failureSuccessRate", Keyword, clientsCnt, requestsCnt, streamsCnt, size), failuresRate);
            _monitor.GetMeasurementDetails();
            if (Interlocked.Read(ref succ) != requestsCnt)
                context.Fail(reason: "There were errors or not all requests completed.");
            else
                context.Success();
        }
    }
}