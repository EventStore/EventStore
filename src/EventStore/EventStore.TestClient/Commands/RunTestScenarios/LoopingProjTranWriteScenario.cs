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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class LoopingProjTranWriteScenario : ProjectionsScenarioBase
    {
        private readonly TimeSpan _executionPeriod;

        private Dictionary<string, string> _transactionsPool;
        private readonly Random _random;

        public LoopingProjTranWriteScenario(Action<IPEndPoint, byte[]> directSendOverTcp, 
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
            _transactionsPool = new Dictionary<string, string>();
            _random = new Random();
        }

        private IEvent CreateEventA(int version)
        {
            var @event = new JsonEventContainer(new VersionnedEventA(version));
            return @event;
        }

        private IEvent CreateEventB(int version)
        {
            var @event = new JsonEventContainer(new VersionnedEventB(version));
            return @event;
        }

        internal class VersionnedEvent
        {
            public readonly int Version;

            public VersionnedEvent(int version)
            {
                Version = version;
            }
        }

        internal class VersionnedEventA : VersionnedEvent
        {
            public VersionnedEventA(int version) : base(version)
            {
            }
        }

        internal class VersionnedEventB : VersionnedEvent
        {
            public VersionnedEventB(int version) : base(version)
            {
            }
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

                InnerRun(runIndex);
                runIndex += 1;
            }
        }

        private void InnerRun(int runIndex)
        {
            var nodeId = StartNode();

            try
            {
                EnableProjectionByCategory();

                var streamA = string.Format("numberedevents{0}-stream_a", runIndex);
                var streamB = string.Format("numberedevents{0}-stream_b", runIndex);

                var store = GetConnection();

                store.CreateStream(streamA, Guid.NewGuid(), true, new byte[0]);
                store.CreateStream(streamB, Guid.NewGuid(), true, new byte[0]);

                var writtenCountA = 0;
                var writtenCountB = 0;
                while (writtenCountA + writtenCountB < EventsPerStream)
                {
                    var batchSizeA = _random.Next(1, EventsPerStream / 10);
                    var batchSizeB = _random.Next(1, EventsPerStream / 10);

                    var transactionA = store.StartTransaction(streamA, ExpectedVersion.Any);

                    var w1 = WriteTransactionData(transactionA.TransactionId, transactionA.Stream, writtenCountA, batchSizeA, CreateEventA);
                    w1.Wait();

                    var transactionB = store.StartTransaction(streamB, ExpectedVersion.Any);
                    var w2 = WriteTransactionData(transactionB.TransactionId, transactionB.Stream, writtenCountB, batchSizeB, CreateEventB);
                    w2.Wait();

                    var cB = CommitTransaction(transactionB);
                    cB.Wait();

                    var cA = CommitTransaction(transactionA);
                    cA.Wait();

                    writtenCountA += batchSizeA;
                    writtenCountB += batchSizeB;
                }

                var projectionName = string.Format("NumberedByType{0}", runIndex);
                var projection = string.Format(@"
                fromCategory('numberedevents{0}')
                    .when({{
                        $init: function() {{ 
                            return {{aVer:0, aList:'', bVer:0, bList:''}}; 
                        }},
                        VersionnedEventA: function(state, event) {{
                            state.aVer += 1;
                            if (state.aVer != event.body.version) {{
                                throw JSON.stringify({{
                                    message: 'Version in A is incorrect. ',
                                    stream: event.streamId,
                                    seqNumber: event.sequenceNumber,
                                    streamType: streamType,
                                    eventInternalVer: event.body.version,
                                    detailsAVer: state.aVer,
                                    detailsBVer: state.bVer}});
                            }}
                            return state;
                        }},
                        VersionnedEventB: function(state, event) {{  
                            state.bVer += 1;
                            if (state.bVer != event.body.version) {{
                                throw JSON.stringify({{
                                    message: 'Version in B is incorrect. ',
                                    stream: event.streamId,
                                    seqNumber: event.sequenceNumber,
                                    streamType: streamType,
                                    eventInternalVer: event.body.version,
                                    detailsAVer: state.aVer,
                                    detailsBVer: state.bVer }});
                            }}
                            return state;
                        }}
                    }})   
", runIndex);

                var projectionManager = GetProjectionsManager();
                projectionManager.CreateContinuous(projectionName, projection);

                WaitAndCheckIfIsFaulted(store, projectionName);

                Log.Debug(string.Format("Done iteration {0}", runIndex));
            }
            finally
            {
                KillNode(nodeId);
            }
        }

        private Task<object> WriteTransactionData(long transactionId, string stream, int startingVersion, int eventCount, Func<int, IEvent> createEvent)
        {
            Log.Info("Starting to write {0} events in tran {1} to [{2}]", eventCount, transactionId, stream);

            var store = GetConnection();

            var resSource = new TaskCompletionSource<object>();

            Action<Task> fail = prevTask =>
            {
                Log.Info("WriteEventsInTransactionalWay for stream {0} failed.", stream);
                resSource.SetException(prevTask.Exception);
            };

            int version = startingVersion;

            Action<Task> writeTransactionEvent = null;
            writeTransactionEvent = _ =>
            {
                if (version == startingVersion + eventCount)
                {   
                    resSource.SetResult(null);
                    return;
                }

                version += 1;

                var writeTask = store.TransactionalWriteAsync(transactionId, stream, new[] { createEvent(version) });
                writeTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
                writeTask.ContinueWith(writeTransactionEvent, TaskContinuationOptions.OnlyOnRanToCompletion);
            };

            Task.Factory.StartNew(() => writeTransactionEvent(null));

            return resSource.Task;
        }

        private Task<object> CommitTransaction(EventStoreTransaction transaction)
        {
            var store = GetConnection();

            var resSource = new TaskCompletionSource<object>();

            Action<Task> fail = prevTask =>
            {
                Log.Info("WriteEventsInTransactionalWay for stream {0} failed.", transaction.Stream);
                resSource.SetException(prevTask.Exception);
            };

            var commitTask = store.CommitTransactionAsync(transaction.TransactionId, transaction.Stream);
            commitTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
            commitTask.ContinueWith(t =>
            {
                Log.Info("Committed tran {0} in [{1}]", transaction.TransactionId, transaction.Stream);
                resSource.SetResult(null);
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            return resSource.Task;
        }

        private void WaitAndCheckIfIsFaulted(EventStoreConnection store, string projectionName)
        {
            var stopWatch = new Stopwatch();
            
            var waitDuration = TimeSpan.FromMilliseconds(20 * 1000 + 5 * Streams * EventsPerStream);
            while (stopWatch.Elapsed < waitDuration )
            {
                string reason;
                var failed = GetProjectionIsFaulted(projectionName, out reason);
                if (failed)
                {
                    var message = string.Format("Projection {0} failed, reason:\n{1}", projectionName, reason);
                    throw new ApplicationException(message);
                }

                var position = GetProjectionPosition(projectionName);
                if (position >= EventsPerStream)
                {
                    Log.Debug("Expected position reached, done.");
                    break;
                }

                Thread.Sleep(2000);
            }
            
        }
    }
}