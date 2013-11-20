﻿// Copyright (c) 2012, Event Store LLP
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
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.TestClient.Commands.DvuBasic;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class ProjForeachForcedCommonNameScenario : ProjectionsScenarioBase
    {
        private int _iterationCode = 0;
        private TimeSpan _executionPeriod;

        public ProjForeachForcedCommonNameScenario(Action<IPEndPoint, byte[]> directSendOverTcp, int maxConcurrentRequests, int connections, int streams, int eventsPerStream, int streamDeleteStep, TimeSpan executionPeriod, string dbParentPath, NodeConnectionInfo customNode)
            : base(directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep, dbParentPath, customNode)
        {
            _executionPeriod = executionPeriod;
        }

        private EventData CreateBankEvent(int version)
        {
            var accountObject = BankAccountEventFactory.CreateAccountObject(version);
            var @event = BankAccountEvent.FromEvent(accountObject);
            return @event;
        }

        protected virtual int GetIterationCode()
        {
            return _iterationCode;
        }

        protected virtual bool ShouldRestartNode
        {
            get { return true; }
        }

        protected override void RunInternal()
        {
            var started = DateTime.UtcNow;

            while ((DateTime.UtcNow - started) < _executionPeriod)
            {
                var msg = string.Format("=================== Start run #{0}, elapsed {1} of {2} minutes, {3} =================== ",
                                        GetIterationCode(),
                                        (int)(DateTime.Now - started).TotalMinutes,
                                        _executionPeriod.TotalMinutes,
                                        GetType().Name);
                Log.Info(msg);
                Log.Info("##teamcity[message '{0}']", msg);

                
                InnerRun();
                _iterationCode += 1;
            }
        }

        private void InnerRun()
        {

            int nodeProcessId = -1;

            if (ShouldRestartNode)
                nodeProcessId = StartNode();

            EnableProjectionByCategory();

            var sumCheckForBankAccounts = CreateSumCheckForBankAccounts("sumCheckForBankAccounts", "1");
            var sumCheckForBankAccounts2 = CreateSumCheckForBankAccounts("sumCheckForBankAccounts", "2");
            
            var projections = new[] { sumCheckForBankAccounts, sumCheckForBankAccounts2 };

            var writeTask = WriteData();

            bool failed = false;
            string failReason = null;

            var isWatchStarted = false;
            var expectedAllEventsCount = Streams + (Streams * EventsPerStream);
            
            var stopWatch = new Stopwatch();

            var waitDuration = TimeSpan.FromMilliseconds(20 * 1000 + 5 * Streams * EventsPerStream);
            while (stopWatch.Elapsed < waitDuration)
            {
                if (writeTask.IsFaulted)
                    throw new ApplicationException("Failed to write data");

                if (writeTask.IsCompleted && !stopWatch.IsRunning)
                {
                    stopWatch.Start();
                    isWatchStarted = true;
                }

                if (isWatchStarted)
                    stopWatch.Stop();

                failed = CheckIsFaulted(projections, out failReason);
                if (failed)
                    break;

                if (CheckIsCompleted(projections, expectedAllEventsCount))
                    break;

                Thread.Sleep((int)(waitDuration.TotalMilliseconds / 6));

                failed = CheckIsFaulted(projections, out failReason);
                if (failed)
                    break;

                if (ShouldRestartNode)
                {
                    KillNode(nodeProcessId);
                    nodeProcessId = StartNode();
                }

                if (isWatchStarted)
                    stopWatch.Start();
            }

            writeTask.Wait();

            for (int i = 3; i < 11; ++i)
            {
                var newSumCheckForBankAccounts = CreateSumCheckForBankAccounts("sumCheckForBankAccounts", i.ToString());
                Thread.Sleep(200);
                failed = CheckIsFaulted(new[] { newSumCheckForBankAccounts }, out failReason);

                if (failed)
                    break;
            }
            
            if (ShouldRestartNode)
                KillNode(nodeProcessId);

            if (failed)
                throw new ApplicationException(string.Format("Projection failed due to reason: {0}.", failReason));
        }

        private bool CheckIsFaulted(IEnumerable<string> projectionsNames, out string failReason)
        {
            var faulted = false;
            failReason = null;
            foreach (var projectionName in projectionsNames)
            {
                faulted = faulted || GetProjectionIsFaulted(projectionName, out failReason);
            }
            return faulted;
        }

        private bool CheckIsCompleted(IEnumerable<string> projectionsNames, int expectedAllEventsCount)
        {
            var completed = false;
            foreach (var projectionName in projectionsNames)
            {
                var position = GetProjectionPosition(projectionName);
                if (position == expectedAllEventsCount)
                {
                    Log.Debug("Expected position reached in {0}, done.", projectionName);
                    completed = true;
                }
            }
            return completed;
        }

        protected Task WriteData()
        {
            var streams = Enumerable.Range(0, Streams).Select(i => string.Format("bank_account_it{0}-{1}", GetIterationCode(), i)).ToArray();
            var slices = Split(streams, 3);

            var w1 = Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream, CreateBankEvent);
            var w2 = Write(WriteMode.Bucket, slices[1], EventsPerStream, CreateBankEvent);
            var w3 = Write(WriteMode.Transactional, slices[2], EventsPerStream, CreateBankEvent);

            var task = Task.Factory.ContinueWhenAll(new[] { w1, w2, w3 }, Task.WaitAll);
            return task.ContinueWith(x => Log.Info("Data written for iteration {0}.", GetIterationCode()));
        }

        protected string CreateSumCheckForBankAccounts(string projectionName, string suffix = "")
        {
            var fullProjectionName = string.Format("{0}_it{1}_{2}", projectionName, GetIterationCode(), suffix);
            var countItemsProjection = string.Format(@"
                options({{$forceProjectionName: ""{0}_it{1}""}});

                fromCategory('bank_account_it{1}').foreachStream().when({{
                    $init: function() {{ 
                        return {{credited:0, credsum:'', debited:0, debsum:''}}; 
                    }},
                    AccountCredited: function (state, event) {{ 
                        state.credited += event.body.creditedAmount; 
                        /*state.credsum += '#' + event.sequenceNumber + ':' + event.body.creditedAmount + ';'*/ 
                    }},
                    AccountDebited: function (state, event) {{ 
                        state.debited += event.body.debitedAmount; 
                        /*state.debsum += '#' + event.sequenceNumber + ':' + event.body.debitedAmount + ';'*/ 
                    }},
                    AccountCheckPoint: function(state, event) {{ 
                        if (state.credited != event.body.creditedAmount) {{
                            throw JSON.stringify({{
                                message: 'Credited amount is incorrect. ',
                                expected: event.body.creditedAmount,
                                actual: state.credited,
                                stream: event.streamId,
                                ver: event.sequenceNumber,
                                details: state.credsum }});
                        }}
                        if (state.debited != event.body.debitedAmount) {{
                            throw JSON.stringify({{
                                message: 'Debited amount is incorrect. ',
                                expected: event.body.debitedAmount,
                                actual: state.debited,
                                stream: event.streamId,
                                ver: event.sequenceNumber,
                                details: state.debsum }});
                        }}
                        state.success=event.sequenceNumber;
                    }}
                }})
", projectionName, GetIterationCode());

            GetProjectionsManager().CreateContinuous(fullProjectionName, countItemsProjection, AdminCredentials);

            return fullProjectionName;
        }
    }

    internal class ProjForeachForcedCommonNameNoRestartScenario : ProjForeachForcedCommonNameScenario
    {
        public ProjForeachForcedCommonNameNoRestartScenario(Action<IPEndPoint, byte[]> directSendOverTcp, 
                                                            int maxConcurrentRequests, 
                                                            int connections, 
                                                            int streams, 
                                                            int eventsPerStream, 
                                                            int streamDeleteStep, 
                                                            TimeSpan executionPeriod, 
                                                            string dbParentPath,
                                                            NodeConnectionInfo customNode)
            : base(directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep, executionPeriod, dbParentPath, customNode)
        {
        }

        protected override bool ShouldRestartNode
        {
            get { return false; }
        }

        protected override void RunInternal()
        {
            StartNode();
            base.RunInternal();
        }
    }
}