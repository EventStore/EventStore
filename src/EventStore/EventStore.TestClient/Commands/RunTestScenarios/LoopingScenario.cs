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
using System.Net;
using System.Threading.Tasks;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class LoopingScenario : ScenarioBase
    {
        private readonly TimeSpan _executionPeriod;
        
        private readonly Random _rnd = new Random();
        private volatile bool _stopParalleWrites;
        private TimeSpan _startupWaitInterval;

        protected override TimeSpan StartupWaitInterval
        {
            get { return _startupWaitInterval; }
        }

        public LoopingScenario(Action<IPEndPoint, byte[]> directSendOverTcp, 
                               int maxConcurrentRequests, 
                               int connections, 
                               int streams, 
                               int eventsPerStream, 
                               int streamDeleteStep,
                               TimeSpan executionPeriod,
                               string dbParentPath)
            : base(directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep, dbParentPath)
        {
            _executionPeriod = executionPeriod;
            SetStartupWaitInterval(TimeSpan.FromSeconds(10));
        }

        private void SetStartupWaitInterval(TimeSpan interval)
        {
            _startupWaitInterval = interval;
        }

        protected override void RunInternal()
        {
            var stopWatch = Stopwatch.StartNew();

            var runIndex = 0;
            while (stopWatch.Elapsed < _executionPeriod)
            {
                var msg = string.Format("=================== Start run #{0}, elapsed {1} of {2} minutes =================== ",
                                        runIndex,
                                        (int)stopWatch.Elapsed.TotalMinutes,
                                        _executionPeriod.TotalMinutes);
                Log.Info(msg);
                Log.Info("##teamcity[message '{0}']", msg);

                SetStartupWaitInterval(TimeSpan.FromSeconds(10 + (2 * (runIndex % 200))));
                InnerRun(runIndex);
                runIndex += 1;
            }
        }

        protected virtual void InnerRun(int runIndex)
        {
            var nodeProcessId = StartNode();

            var parallelWritesTimeout = TimeSpan.FromMinutes((EventsPerStream / 1000.0) * 7);

            var parallelWriteTask = RunParallelWrites(runIndex);

            var streams = Enumerable.Range(0, Streams).Select(i => FormatStreamName(runIndex, i)).ToArray();
            
            var slices = Split(streams, 3);
            var singleEventSlice = slices.Take(1).SelectMany(x => x).ToArray();
            var batchSlice = slices.Skip(1).Take(1).SelectMany(x => x).ToArray();
            var transSlice = slices.Skip(2).Take(1).SelectMany(x => x).ToArray();

            var wr1 = Write(WriteMode.SingleEventAtTime, singleEventSlice, EventsPerStream);
            var wr2 = Write(WriteMode.Bucket, batchSlice, EventsPerStream);
            var wr3 = Write(WriteMode.Transactional, transSlice, EventsPerStream);

            if (runIndex % 4 == 0)
                Scavenge();

            Task.WaitAll(wr1, wr2, wr3);

            var deleted = streams.Where((s, i) => i % StreamDeleteStep == 0).ToArray();
            DeleteStreams(deleted);

            _stopParalleWrites = true;
            if (!parallelWriteTask.Wait(parallelWritesTimeout))
                throw new ApplicationException("Parallel writes stop timed out.");

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            parallelWriteTask = RunParallelWrites(runIndex);

            var dl1 = CheckStreamsDeleted(deleted);

            var exceptDeleted = streams.Except(deleted).ToArray();

            var readCnt = Math.Min(100, EventsPerStream/3);
            var rd1 = Read(exceptDeleted, @from: 0, count: readCnt + 1);
            var rd2 = Read(exceptDeleted, @from: EventsPerStream - readCnt, count: readCnt + 1);
            var rd3 = Read(exceptDeleted, @from: EventsPerStream / 2, count: Math.Min(readCnt, EventsPerStream - EventsPerStream/2) + 1);

            Log.Info("== READ from picked ALL ==");

            var allExistingStreamsSlice = (from run in Enumerable.Range(0, runIndex + 1)
                                           from streamNum in Enumerable.Range(0, Streams)
                                           where streamNum % StreamDeleteStep != 0
                                           where _rnd.NextDouble() < 0.1
                                           select FormatStreamName(run, streamNum)).ToArray();

            var allDeletedStreamsSlice = (from run in Enumerable.Range(0, runIndex + 1)
                                          from streamNum in Enumerable.Range(0, Streams)
                                          where streamNum % StreamDeleteStep == 0
                                          where _rnd.NextDouble() < 0.1
                                          select FormatStreamName(run, streamNum)).ToArray();

            var prevCheckTasks = new List<Task>();
            if (allExistingStreamsSlice.Length > 0)
            {
                var rd4 = Read(allExistingStreamsSlice, 0, Math.Max(1, EventsPerStream / 5));
                var rd5 = Read(allExistingStreamsSlice, EventsPerStream / 2, Math.Max(1, EventsPerStream / 5));

                prevCheckTasks.Add(rd4);
                prevCheckTasks.Add(rd5);
            }

            if (allDeletedStreamsSlice.Length > 0)
            {
                var dl2 = CheckStreamsDeleted(allDeletedStreamsSlice);
                prevCheckTasks.Add(dl2);
            }

            Task.WaitAll(new [] {dl1, rd1, rd2, rd3}.Union(prevCheckTasks).ToArray());

            _stopParalleWrites = true;
            if (!parallelWriteTask.Wait(parallelWritesTimeout))
                throw new ApplicationException("Parallel writes stop timed out.");

            KillNode(nodeProcessId);
        }

        protected virtual Task RunParallelWrites(int runIndex)
        {
            _stopParalleWrites = false;

            return Task.Factory.StartNew(() =>
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
            }, TaskCreationOptions.LongRunning);
        }

        protected static string FormatStreamName(int runIndex, int i)
        {
            return string.Format("stream-in{0}-{1}", runIndex, i);
        }
    }
}