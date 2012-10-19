using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class LoopingScenario : ScenarioBase
    {
        private readonly TimeSpan _executionPeriod;
        
        protected readonly Random _rnd = new Random();
        protected volatile bool _stopParalleWrites;
        private TimeSpan _startupWaitInterval;

        protected override TimeSpan StartupWaitInterval
        {
            get { return _startupWaitInterval; }
        }

        public LoopingScenario(Action<byte[]> directSendOverTcp, 
                               int maxConcurrentRequests, 
                               int threads, 
                               int streams, 
                               int eventsPerStream, 
                               int streamDeleteStep,
                               TimeSpan executionPeriod) 
            : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep)
        {
            _executionPeriod = executionPeriod;
            SetStartupWaitInterval(TimeSpan.FromSeconds(7));
        }

        private void SetStartupWaitInterval(TimeSpan interval)
        {
            _startupWaitInterval = interval;
        }

        public override void Run()
        {
            var stopWatch = Stopwatch.StartNew();

            var runIndex = 0;
            while (stopWatch.Elapsed < _executionPeriod)
            {
                Log.Info("=================== Start run #{0}, elapsed {1} of {2} minutes =================== ",
                         runIndex,
                         (int)stopWatch.Elapsed.TotalMinutes,
                         _executionPeriod.TotalMinutes);

                SetStartupWaitInterval(TimeSpan.FromSeconds(7 + (2 * runIndex) % 200));
                InnerRun(runIndex);
                runIndex += 1;
            }
        }

        protected virtual void InnerRun(int runIndex)
        {
            const int internalThreadsCount = 2;
            ThreadPool.SetMaxThreads(Threads + internalThreadsCount, Threads + internalThreadsCount);

            var nodeProcessId = StartNode();
            if (runIndex % 2 == 0)
                Scavenge();

            var parallelWritesEvent = RunParallelWrites(runIndex);

            var streams = Enumerable.Range(0, Streams).Select(i => FormatStreamName(runIndex, i)).ToArray();
            var slices = Split(streams, 3);

            Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream);
            Write(WriteMode.Bucket, slices[1], EventsPerStream);
            Write(WriteMode.Transactional, slices[2], EventsPerStream);

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
            Read(exceptDeleted, @from: 0, count: Piece + 1);
            Read(exceptDeleted, @from: EventsPerStream - Piece, count: Piece + 1);
            Read(exceptDeleted, @from: EventsPerStream / 2, count: Math.Min(Piece + 1, EventsPerStream - EventsPerStream / 2));

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
            if (!parallelWritesEvent.WaitOne(60000))
                throw new ApplicationException("Parallel writes stop timed out.");

            KillNode(nodeProcessId);
        }

        protected virtual AutoResetEvent RunParallelWrites(int runIndex)
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
                        
                        var eventsPerStream = EventsPerStream;

                        Write(WriteMode.SingleEventAtTime, parallelStreams, eventsPerStream);
                        Read(parallelStreams, 0, eventsPerStream / 6);
                        Read(parallelStreams, eventsPerStream / 3, eventsPerStream / 6);
                        Read(parallelStreams, eventsPerStream - eventsPerStream / 10, eventsPerStream / 10);
                    }
                    resetEvent.Set();
                });
            return resetEvent;
        }

        protected static string FormatStreamName(int runIndex, int i)
        {
            return string.Format("stream-in{0}-{1}", runIndex, i);
        }
    }
}