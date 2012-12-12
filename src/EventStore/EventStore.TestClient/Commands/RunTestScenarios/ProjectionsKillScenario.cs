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
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Core.Services.Transport.Http.Codecs;
using EventStore.TestClient.Commands.DvuBasic;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class ProjectionsKillScenario : ScenarioBase
    {
        public ProjectionsKillScenario(Action<IPEndPoint, byte[]> directSendOverTcp, int maxConcurrentRequests, int connections, int streams, int eventsPerStream, int streamDeleteStep, string dbParentPath)
            : base(directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep, dbParentPath)
        {
        }

        private IEvent CreateBankEvent(int version)
        {
            var accountObject = BankAccountEventFactory.CreateAccountObject(version);
            var @event = new BankAccountEvent(accountObject);
            return @event;
        }

        protected virtual int GetIterationCode()
        {
            return 0;
        }

        protected override void RunInternal()
        {
            var nodeProcessId = StartNode();

            var countItem = CreateCountItem();
            var sumCheckForBankAccount0 = CreateSumCheckForBankAccount0();

            var writeTask = WriteData();

            var success = false;
            var expectedAllEventsCount = (Streams * EventsPerStream + Streams).ToString();
            var expectedEventsPerStream = EventsPerStream.ToString();

            var isWatchStarted = false;
            var manager = GetProjectionsManager();
            
            var stopWatch = new Stopwatch();
            while (stopWatch.Elapsed < TimeSpan.FromMilliseconds(1000 + 5 * Streams * EventsPerStream))
            {
                if (writeTask.IsFaulted)
                    throw new ApplicationException("Failed to write data");

                if (writeTask.IsCompleted && !stopWatch.IsRunning)
                {
                    stopWatch.Start();
                    isWatchStarted = true;
                }

                success = CheckProjectionState(manager, countItem, "count", x => x == expectedAllEventsCount)
                       && CheckProjectionState(manager, sumCheckForBankAccount0, "success", x => x == expectedEventsPerStream);

                if (success)
                    break;

                if (isWatchStarted)
                    stopWatch.Stop();

                Thread.Sleep(IterationSleepInterval);
                KillNode(nodeProcessId);
                nodeProcessId = StartNode();

                if (isWatchStarted)
                    stopWatch.Start();
            }

            writeTask.Wait();

            KillNode(nodeProcessId);

            if (!success)
                throw new ApplicationException(string.Format("Projections did not complete with expected result in time"));
        }

        protected virtual TimeSpan IterationSleepInterval
        {
            get { return TimeSpan.FromSeconds(4); }
        }

        protected Task WriteData()
        {
            var streams = Enumerable.Range(0, Streams).Select(i => string.Format("bank_account_it{0}-{1}", GetIterationCode(), i)).ToArray();
            var slices = Split(streams, 3);

            var w1 = Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream, CreateBankEvent);
            var w2 = Write(WriteMode.Bucket, slices[1], EventsPerStream, CreateBankEvent);
            var w3 = Write(WriteMode.Transactional, slices[2], EventsPerStream, CreateBankEvent);

            var task = Task.Factory.ContinueWhenAll(new [] {w1, w2, w3}, Task.WaitAll);
            task.ContinueWith(x => Log.Info("Data written."));
            return task;
        }

        protected bool CheckProjectionState(ProjectionsManager manager, string projectionName, string key, Func<string, bool> checkValue)
        {
            var state = GetProjectionState(manager, projectionName);
            string value;
            return state != null && state.Count > 0 && state.TryGetValue(key, out value) && checkValue(value);
        }

        protected T GetProjectionStateValue<T>(ProjectionsManager manager, string projectionName, string key, Func<string, T> convert, T defaultValue)
        {
            var result = defaultValue;

            var state = GetProjectionState(manager, projectionName);
            string value;
            if (state != null && state.Count > 0 && state.TryGetValue(key, out value))
            {
                result = convert(value);
            }
            return result;
        }

        protected static Dictionary<string, string> GetProjectionState(ProjectionsManager manager, string projectionName)
        {
            var rawState = GetProjectionStateSafe(manager, projectionName);

            Log.Info("Raw {0} state: {1}", projectionName, rawState);

            if (string.IsNullOrEmpty(rawState))
                return null;

            var state = Codec.Json.From<Dictionary<string, string>>(rawState);
            return state;
        }

        protected static string GetProjectionStateSafe(ProjectionsManager manager, string projectionName)
        {
            string rawState;
            try
            {
                rawState = manager.GetState(projectionName);
            }
            catch (Exception ex)
            {
                rawState = null;
                Log.InfoException(ex, "Failed to get projection state");
            }
            return rawState;
        }

        protected string CreateCountItem()
        {
            var projectionManager = GetProjectionsManager();

            projectionManager.Enable("$by_category");

            string countItemsProjectionName = string.Format("CountItems_it{0}", GetIterationCode());
            string countItemsProjection = string.Format(@"
                fromCategory('bank_account_it{0}').when({{
                $init: function() {{ return {{c:0}}; }},
                AccountCredited: function (state, event) {{ 
                                        state.c += 1; 
                                    }},
                AccountDebited: function (state, event) {{ 
                                        state.c+= 1; 
                                    }},
                AccountCheckPoint: function (state, event) {{ 
                                        state.c+= 1; 
                                    }}
                }})
", GetIterationCode());

            projectionManager.CreateContinuous(countItemsProjectionName, countItemsProjection);
            return countItemsProjectionName;
        }

        protected string CreateSumCheckForBankAccount0()
        {
            string countItemsProjectionName = string.Format("CheckSumsInAccounts_it{0}", GetIterationCode());
            string countItemsProjection = string.Format(@"
                fromStream('bank_account_it{0}-0').when({{
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
", GetIterationCode());
            
            GetProjectionsManager().CreateContinuous(countItemsProjectionName, countItemsProjection);

            return countItemsProjectionName;
        }
    }
}