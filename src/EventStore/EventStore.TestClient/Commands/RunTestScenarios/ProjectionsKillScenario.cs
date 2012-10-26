using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Core.Services.Transport.Http;
using EventStore.TestClient.Commands.DvuBasic;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class BankAccountEvent : IEvent
    {
        public Guid EventId { get; private set; }
        public string Type { get; private set; }
        public byte[] Data { get; private set; }
        public byte[] Metadata { get; private set; }

        public BankAccountEvent(object accountObject)
        {
            if (accountObject == null)
                throw new ArgumentNullException("accountObject");

            EventId = Guid.NewGuid();
            Type = accountObject.GetType().Name;

            Data = Encoding.UTF8.GetBytes(Codec.Json.To(accountObject));
            Metadata = Encoding.UTF8.GetBytes(Codec.Json.To(new Dictionary<string, object> { { "IsEmpty", true } }));
        }
    }

    internal class ProjectionsKillScenario : ScenarioBase
    {
        public ProjectionsKillScenario(Action<IPEndPoint, byte[]> directSendOverTcp,
                                       int maxConcurrentRequests,
                                       int connections,
                                       int streams,
                                       int eventsPerStream,
                                       int streamDeleteStep)
            : base(directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep)
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
            var store = GetConnection();
            
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

                success = CheckProjectionState(store, countItem, "count", x => x == expectedAllEventsCount)
                       && CheckProjectionState(store, sumCheckForBankAccount0, "success", x => x == expectedEventsPerStream);

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
            var streams = Enumerable.Range(0, Streams).Select(i => string.Format("bank-account-it{0}-{1}", GetIterationCode(), i)).ToArray();
            var slices = Split(streams, 3);

            var w1 = Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream, CreateBankEvent);
            var w2 = Write(WriteMode.Bucket, slices[1], EventsPerStream, CreateBankEvent);
            var w3 = Write(WriteMode.Transactional, slices[2], EventsPerStream, CreateBankEvent);

            var task = Task.Factory.ContinueWhenAll(new [] {w1, w2, w3}, Task.WaitAll);
            task.ContinueWith(x => Log.Info("Data written."));
            return task;
        }

        protected bool CheckProjectionState(EventStoreConnection store, string projectionName, string key, Func<string, bool> checkValue)
        {
            var rawState = GetProjectionStateSafe(store, projectionName);

            Log.Info("Raw {0} state: {1}", projectionName, rawState);

            if (string.IsNullOrEmpty(rawState))
                return false;
            
            var state = Codec.Json.From<Dictionary<string, string>>(rawState);
            string value;
            return state != null && state.Count > 0 && state.TryGetValue(key, out value) && checkValue(value);
        }

        private static string GetProjectionStateSafe(EventStoreConnection store, string projectionName)
        {
            string rawState;
            try
            {
                rawState = store.Projections.GetState(projectionName);
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
            string countItemsProjectionName = string.Format("CountItems_it{0}", GetIterationCode());
            const string countItemsProjection = @"
                fromAll().whenAny(
                    function(state, event) {
                        if (event.streamId.indexOf('bank-account-') != 0) return state;
                        if (state.count == undefined) state.count = 0;
                        state.count += 1;
                        return state;
                    });
";
            GetConnection().Projections.CreatePersistent(countItemsProjectionName, countItemsProjection);
            return countItemsProjectionName;
        }

        protected string CreateSumCheckForBankAccount0()
        {
            string countItemsProjectionName = string.Format("CheckSumsInAccounts_it{0}", GetIterationCode());
            string countItemsProjection = string.Format(@"
                fromAll().whenAny(
                    function(state, event) {{
                        if (event.streamId.indexOf('bank-account-it{0}-0') != 0) return state;
                        if (event.eventType.indexOf('AccountCredited') == 0)
                        {{
                            if (state.credited == undefined) {{ state.credited = 0; state.credsum = '' }};
                            state.credited += event.body.creditedAmount;
                            // state.credsum += '#' + event.sequenceNumber + ':' + event.body.creditedAmount + ';';
                        }}

                        if (event.eventType.indexOf('AccountDebited') == 0)
                        {{
                            if (state.debited == undefined) {{ state.debited = 0; state.debsum = '' }};
                            state.debited += event.body.debitedAmount;
                            // state.debsum += '#' + event.sequenceNumber + ':' + event.body.debitedAmount + ';';
                        }}

                        if (event.eventType.indexOf('AccountCheckPoint') == 0)
                        {{
                            if (state.debited == undefined) return state;
                            if (state.credited == undefined) return state;
                            
                            if (state.credited != event.body.creditedAmount) throw 'Credited amount is incorrect (expected: ' + event.body.creditedAmount + ', actual: ' + state.credited + ' stream: ' + event.streamId + ' ver: ' + event.sequenceNumber + ': details: ' + state.credsum + ')'
                            if (state.debited != event.body.debitedAmount) throw 'Debited amount is incorrect (expected: ' + event.body.debitedAmount + ', actual: ' + state.debited + ' stream: ' + event.streamId + ' ver: ' + event.sequenceNumber + ': details: ' + state.debsum + ')'

                            state.success=event.sequenceNumber;
                        }}
                        
                        return state;
                    }});
", GetIterationCode());
            
            GetConnection().Projections.CreatePersistent(countItemsProjectionName, countItemsProjection);

            return countItemsProjectionName;
        }
    }

    internal class LoopingProjectionKillScenario : ProjectionsKillScenario
    {
        private static readonly TimeSpan _iterationSleepInterval = TimeSpan.FromMinutes(10);
        private TimeSpan _executionPeriod;

        public LoopingProjectionKillScenario(Action<IPEndPoint, byte[]> directSendOverTcp, 
            int maxConcurrentRequests, 
            int connections, 
            int streams, 
            int eventsPerStream, 
            int streamDeleteStep,
            TimeSpan executionPeriod) 
            : base(directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep)
        {
            _executionPeriod = executionPeriod;
        }

        protected override TimeSpan IterationSleepInterval
        {
            get { return _iterationSleepInterval; }
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

            var stopWatch = Stopwatch.StartNew();

            while (stopWatch.Elapsed < _executionPeriod)
            {
                Log.Info("=================== Start run #{0}, elapsed {1} of {2} minutes =================== ",
                         GetIterationCode(),
                         (int)stopWatch.Elapsed.TotalMinutes,
                         _executionPeriod.TotalMinutes);

                var iterationTask = RunIteration();

                Thread.Sleep(TimeSpan.FromMinutes(0.5));

                KillNode(nodeProcessId);
                nodeProcessId = StartNode();

                iterationTask.Wait();

                SetNextIterationCode();
            }
        }

        private Task RunIteration()
        {
            var countItem = CreateCountItem();
            var sumCheckForBankAccount0 = CreateSumCheckForBankAccount0();

            var writeTask = WriteData();

            var expectedAllEventsCount = (Streams * EventsPerStream + Streams).ToString();
            var expectedEventsPerStream = EventsPerStream.ToString();

            var store = GetConnection();

            var successTask = Task.Factory.StartNew<bool>(() => 
                {
                    var success = false;
                    var stopWatch = new Stopwatch();
                    while (stopWatch.Elapsed < TimeSpan.FromMilliseconds(10 * (Streams * EventsPerStream + Streams)))
                    {
                        if (writeTask.IsFaulted)
                            throw new ApplicationException("Failed to write data");

                        if (writeTask.IsCompleted && !stopWatch.IsRunning)
                        {
                            stopWatch.Start();
                        }

                        success = CheckProjectionState(store, countItem, "count", x => x == expectedAllEventsCount)
                               && CheckProjectionState(store, sumCheckForBankAccount0, "success", x => x == expectedEventsPerStream);

                        if (success)
                            break;

                        Thread.Sleep(500);

                    }
                    return success;
                    
                });

            return Task.Factory.ContinueWhenAll(new [] { writeTask, successTask }, tasks => { Log.Info("Iteration {0} tasks completed", GetIterationCode()); Task.WaitAll(tasks); Log.Info("Iteration {0} successfull", GetIterationCode()); });
        }
    }
}