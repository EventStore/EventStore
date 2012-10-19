using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
            ThreadPool.SetMaxThreads(50, 50);

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
            var nodeProcessId = StartNode();
            if (runIndex % 2 == 0)
                Scavenge();

            var parallelWritesEvent = RunParallelWrites(runIndex);

            var streams = Enumerable.Range(0, Streams).Select(i => FormatStreamName(runIndex, i)).ToArray();
            var slices = Split(streams, 3);

            var wr1 = Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream);
            var wr2 = Write(WriteMode.Bucket, slices[1], EventsPerStream);
            var wr3 = Write(WriteMode.Transactional, slices[2], EventsPerStream);
            Task.WaitAll(wr1, wr2, wr3);

            var deleted = streams.Where((s, i) => i % StreamDeleteStep == 0).ToArray();
            DeleteStreams(deleted);

            _stopParalleWrites = true;
            if (!parallelWritesEvent.WaitOne(60000))
                throw new ApplicationException("Parallel writes stop timed out.");

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            parallelWritesEvent = RunParallelWrites(runIndex);

            var dl1 = CheckStreamsDeleted(deleted);

            var exceptDeleted = streams.Except(deleted).ToArray();
            var rd1 = Read(exceptDeleted, @from: 0, count: Piece + 1);
            var rd2 = Read(exceptDeleted, @from: EventsPerStream - Piece, count: Piece + 1);
            var rd3 = Read(exceptDeleted, @from: EventsPerStream / 2, count: Math.Min(Piece + 1, EventsPerStream - EventsPerStream / 2));

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

            var rd4 = Read(pickedFromAllStreamsForRead, 0, EventsPerStream / 5);
            var rd5 = Read(pickedFromAllStreamsForRead, EventsPerStream / 2, EventsPerStream / 5);

            var dl2 = CheckStreamsDeleted(pickedFromAllStreamsDeleted);

            Task.WaitAll(dl1, dl2, rd1, rd2, rd3, rd4, rd5);

            _stopParalleWrites = true;
            if (!parallelWritesEvent.WaitOne(60000))
                throw new ApplicationException("Parallel writes stop timed out.");

            KillNode(nodeProcessId);
        }

        protected virtual AutoResetEvent RunParallelWrites(int runIndex)
        {
            _stopParalleWrites = false;
            var resetEvent = new AutoResetEvent(false);

            // TODO AN: fix this
            resetEvent.Set();
            return resetEvent;


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

                    var wr = Write(WriteMode.SingleEventAtTime, parallelStreams, EventsPerStream);
                    wr.Wait();

                    var rd1 = Read(parallelStreams, 0, EventsPerStream / 6);
                    var rd2 = Read(parallelStreams, EventsPerStream / 3, EventsPerStream / 6);
                    var rd3 = Read(parallelStreams, EventsPerStream - EventsPerStream / 10, EventsPerStream / 10);
                    Task.WaitAll(rd1, rd2, rd3);
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