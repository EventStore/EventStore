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
    internal class ProjectionWrongTagCheck : ProjectionsKillScenario
    {
        private TimeSpan _executionPeriod;

        public ProjectionWrongTagCheck(Action<IPEndPoint, byte[]> directSendOverTcp, int maxConcurrentRequests, int connections, int streams, int eventsPerStream, int streamDeleteStep, TimeSpan executionPeriod, string dbParentPath)
            : base(directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep, dbParentPath)
        {
            _executionPeriod = executionPeriod;
        }

        private int _iterationCode = 0;
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
            KillNode(nodeProcessId);

            var stopWatch = Stopwatch.StartNew();

            while (stopWatch.Elapsed < _executionPeriod)
            {
                var msg = string.Format("=================== Start run #{0}, elapsed {1} of {2} minutes =================== ",
                           GetIterationCode(),
                           (int)stopWatch.Elapsed.TotalMinutes,
                           _executionPeriod.TotalMinutes);
                Log.Info(msg);
                Log.Info("##teamcity[message '{0}']", msg);

                var iterationTask = RunIteration();
                iterationTask.Wait();

                SetNextIterationCode();
            }
        }

        private Task RunIteration()
        {
            var nodeProcessId = StartNode();


            var sumCheckForBankAccount0 = CreateSumCheckForBankAccount0();

            var writeTask = WriteData();

            var expectedEventsPerStream = EventsPerStream.ToString();

            var successTask = Task.Factory.StartNew<bool>(() =>
            {
                var store = GetConnection();
                var manager = GetProjectionsManager();

                var success = true;
                var stopWatch = new Stopwatch();
                
                var wasLessLastTime = false;
                while (stopWatch.Elapsed < TimeSpan.FromMilliseconds(10 * (Streams * EventsPerStream + Streams)))
                {
                    if (!wasLessLastTime)
                    {
                        KillNode(nodeProcessId);
                        nodeProcessId = StartNode();
                    }

                    if (writeTask.IsFaulted)
                        throw new ApplicationException("Failed to write data");

                    if (writeTask.IsCompleted && !stopWatch.IsRunning)
                    {
                        stopWatch.Start();
                    }

                    var count1 = GetProjectionStateValue(sumCheckForBankAccount0, "success", int.Parse, -1);
                    for (var i = 0; i < 5; ++i)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        var count2 = GetProjectionStateValue(sumCheckForBankAccount0, "success", int.Parse, -1);

                        if (count1 > count2)
                        {
                            if (wasLessLastTime)
                            {
                                success = false;
                                break;
                            }
                            wasLessLastTime = true;
                        }

                        count1 = count2;
                    }

                    if (!success)
                        break;

                    if (CheckProjectionState(sumCheckForBankAccount0, "success", x => x == expectedEventsPerStream))
                        break;
                }

                KillNode(nodeProcessId);

                return success;

            });

            return Task.Factory.ContinueWhenAll(new[] { writeTask, successTask }, tasks => { Log.Info("Iteration {0} tasks completed", GetIterationCode()); Task.WaitAll(tasks); Log.Info("Iteration {0} successfull", GetIterationCode()); });
        }
    }
}