using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class SyncScenario : LoopingScenario
    {
        public SyncScenario(Action<byte[]> directSendOverTcp,
                            int maxConcurrentRequests,
                            int threads,
                            int streams,
                            int eventsPerStream,
                            int streamDeleteStep,
                            TimeSpan executionPeriod)
            : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep, executionPeriod)
        {
            
        }

        protected override void InnerRun(int runIndex)
        {
            const int internalThreadsCount = 2;
            ThreadPool.SetMaxThreads(Threads + internalThreadsCount, Threads + internalThreadsCount);

            var nodeProcessId = StartNode();
            if (runIndex % 2 == 0)
                Scavenge();

            var parallelWritesEvent = RunParallelWrites(runIndex);

            var streams = Enumerable.Range(0, Streams).Select(i => FormatStreamName(runIndex, i)).ToArray();
            var slices = Split(streams, 3);

            Write(WriteMode.SingleEventAtTimeSync, slices[0], EventsPerStream);
            Write(WriteMode.BucketSync, slices[1], EventsPerStream);
            Write(WriteMode.TransactionalSync, slices[2], EventsPerStream);

            var deleted = streams.Where((s, i) => i % StreamDeleteStep == 0).ToArray();
            DeleteStreams(deleted);

            _stopParalleWrites = true;
            if (!parallelWritesEvent.WaitOne(60000))
                throw new ApplicationException("Parallel writes stop timed out.");

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            parallelWritesEvent = RunParallelWrites(runIndex);

            CheckStreamsDeleted(deleted);

            var exceptDeleted = streams.Except(deleted).ToArray();
            Read(exceptDeleted, from: 0, count: Piece + 1);
            Read(exceptDeleted, from: EventsPerStream - Piece, count: Piece + 1);
            Read(exceptDeleted, from: EventsPerStream / 2, count: Math.Min(Piece + 1, EventsPerStream - EventsPerStream / 2));

            Log.Info("== READ from picked ALL ==");

            var pickedFromAllStreamsForRead = Enumerable.Range(0, runIndex)
                                              .SelectMany(run => Enumerable.Range(0, Streams)
                                              .Where(s => s % StreamDeleteStep != 0)
                                              .Select(s => FormatStreamName(run, s)))
                                              .Where(x => _rnd.Next(100) > (10 + 1000 / runIndex)).ToArray();

            var pickedFromAllStreamsDeleted = Enumerable.Range(0, runIndex)
                                              .SelectMany(run => Enumerable.Range(0, Streams)
                                              .Where(s => s % StreamDeleteStep == 0)
                                              .Select(s => FormatStreamName(run, s)))
                                              .Where(x => _rnd.Next(100) > (5 + 1000 / runIndex)).ToArray();

            Read(pickedFromAllStreamsForRead, 0, EventsPerStream / 5);
            Read(pickedFromAllStreamsForRead, EventsPerStream / 2, EventsPerStream / 5);

            CheckStreamsDeleted(pickedFromAllStreamsDeleted);

            _stopParalleWrites = true;
            if (!parallelWritesEvent.WaitOne(180000))
                throw new ApplicationException("Parallel writes stop timed out.");

            KillNode(nodeProcessId);
        }

        protected override AutoResetEvent RunParallelWrites(int runIndex)
        {
            _stopParalleWrites = false;
            var resetEvent = new AutoResetEvent(false);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (!_stopParalleWrites)
                {
                    var parallelStreams = Enumerable.Range(0, 2)
                    .Select(x => string.Format("parallel-write-stream-in{0}-{1}-{2}",
                        runIndex,
                        x,
                        string.Format("rnd{0}-{1}", _rnd.Next(), DateTime.UtcNow.Ticks)))
                    .ToArray();

                    Thread.Sleep(1);

                    Write(WriteMode.SingleEventAtTimeSync, parallelStreams, EventsPerStream);
                    Read(parallelStreams, 0, EventsPerStream / 6);
                    Read(parallelStreams, EventsPerStream / 3, EventsPerStream / 6);
                    Read(parallelStreams, EventsPerStream - EventsPerStream / 10, EventsPerStream / 10);
                }
                resetEvent.Set();
            });
            return resetEvent;
        }
    }
}
