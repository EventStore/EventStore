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
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class LoopingProjectionKillScenario : ProjectionsKillScenario
    {
        private TimeSpan _executionPeriod;

        private int _iterationCode;
        private readonly TimeSpan _iterationLoopDuration;
        private readonly TimeSpan _firstKillInterval;

        public LoopingProjectionKillScenario(Action<IPEndPoint, byte[]> directSendOverTcp, int maxConcurrentRequests, int connections, int streams, int eventsPerStream, int streamDeleteStep, TimeSpan executionPeriod, string dbParentPath) 
            : base(directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep, dbParentPath)
        {
            _executionPeriod = executionPeriod;

            _iterationLoopDuration = TimeSpan.FromMilliseconds(10 * (Streams * EventsPerStream + Streams) + 20 * 1000);
            _firstKillInterval = TimeSpan.FromSeconds(_iterationLoopDuration.TotalSeconds / 2);
        }

        protected override int GetIterationCode()
        {
            return _iterationCode;
        }

        protected void SetNextIterationCode()
        {
            _iterationCode += 1;
        }

        protected override void RunInternal()
        {
            var nodeProcessId = StartNode();
            EnableProjectionByCategory();

            var stopWatch = Stopwatch.StartNew();

            while (stopWatch.Elapsed < _executionPeriod)
            {

                var msg = string.Format("=================== Start run #{0}, elapsed {1} of {2} minutes, {3} =================== ",
                                        GetIterationCode(),
                                        (int)stopWatch.Elapsed.TotalMinutes,
                                        _executionPeriod.TotalMinutes,
                                        GetType().Name);
                Log.Info(msg);
                Log.Info("##teamcity[message '{0}']", msg);

                var iterationTask = RunIteration();

                Thread.Sleep(_firstKillInterval);

                KillNode(nodeProcessId);
                nodeProcessId = StartNode();

                if (!iterationTask.Wait(_iterationLoopDuration))
                    throw new TimeoutException("Iteration execution timeout.");

                if (iterationTask.Result != true)
                    throw new ApplicationException("Iteration faulted.", iterationTask.Exception);

                SetNextIterationCode();
            }
        }

        private Task<bool> RunIteration()
        {
            var countItem = CreateCountItem();
            var sumCheckForBankAccount0 = CreateSumCheckForBankAccount0();

            var writeTask = WriteData();

            var expectedAllEventsCount = (Streams * EventsPerStream).ToString();
            var expectedEventsPerStream = EventsPerStream.ToString();

            var successTask = Task.Factory.StartNew(() => 
                {
                    var success = false;
                    var stopWatch = new Stopwatch();
                    while (stopWatch.Elapsed < _iterationLoopDuration)
                    {
                        if (writeTask.IsFaulted)
                            throw new ApplicationException("Failed to write data");

                        if (writeTask.IsCompleted && !stopWatch.IsRunning)
                        {
                            stopWatch.Start();
                        }

                        success = CheckProjectionState(countItem, "count", x => x == expectedAllEventsCount)
                               && CheckProjectionState(sumCheckForBankAccount0, "success", x => x == expectedEventsPerStream);

                        if (success)
                            break;

                        Thread.Sleep(4000);

                    }
                    return success;
                    
                });

            writeTask.Wait();

            return successTask;
        }
    }
}