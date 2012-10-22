using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public ProjectionsKillScenario(Action<byte[]> directSendOverTcp,
                                          int maxConcurrentRequests,
                                          int threads,
                                          int streams,
                                          int eventsPerStream,
                                          int streamDeleteStep)
            : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep)
        {
        }

        private IEvent CreateBankEvent(int version)
        {
            var accountObject = BankAccountEventFactory.CreateAccountObject(version);
            var @event = new BankAccountEvent(accountObject);
            return @event;
        }

        public override void Run()
        {
            var nodeProcessId = StartNode();

            var countItem = CreateCountItem();
            var sumCheckForBankAccount0 = CreateSumCheckForBankAccount0();

            var writeTask = WriteData();

            var success = false;
            var expectedAllEventsCount = (Streams * EventsPerStream + Streams).ToString();
            var expectedEventsPerStream = EventsPerStream.ToString();
            var restartTimeMilliseconds = 0;
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
            {
                var stopWatch = new Stopwatch();
                while (stopWatch.Elapsed < TimeSpan.FromMilliseconds(1000 + 10 * Streams * EventsPerStream + restartTimeMilliseconds))
                {
                    if (writeTask.IsFaulted)
                        throw new ApplicationException("Failed to write data");

                    if (writeTask.IsCompleted && !stopWatch.IsRunning)
                        stopWatch.Start();

                    success = CheckProjectionState(store, countItem, "count", x => x == expectedAllEventsCount)
                              && CheckProjectionState(store, sumCheckForBankAccount0, "success", x => x == expectedEventsPerStream);

                    if (success)
                        break;

                    Thread.Sleep(4000);
                    if (stopWatch.IsRunning)
                        restartTimeMilliseconds += 6000;

                    KillNode(nodeProcessId);
                    nodeProcessId = StartNode();
                }
            }

            writeTask.Wait();

            KillNode(nodeProcessId);

            if (!success)
                throw new ApplicationException(string.Format("Projections did not complete with expected result in time"));
        }

        private Task WriteData()
        {
            var streams = Enumerable.Range(0, Streams).Select(i => string.Format("bank-account-{0}", i)).ToArray();
            var slices = Split(streams, 3);

            var w1 = Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream, CreateBankEvent);
            var w2 = Write(WriteMode.Bucket, slices[1], EventsPerStream, CreateBankEvent);
            var w3 = Write(WriteMode.Transactional, slices[2], EventsPerStream, CreateBankEvent);

            var task = Task.Factory.ContinueWhenAll(new [] {w1, w2, w3}, Task.WaitAll);
            task.ContinueWith(x => Log.Info("Data written."));
            return task;
        }

        private bool CheckProjectionState(EventStoreConnection store, string projectionName, string key, Func<string, bool> checkValue)
        {
            var rawState = store.Projections.GetState(projectionName);
            Log.Info("Raw {0} state: {1}", projectionName, rawState);
            var state = Codec.Json.From<Dictionary<string, string>>(rawState);
            string value;
            return state.Count > 0 && state.TryGetValue(key, out value) && checkValue(value);
        }

        private string CreateCountItem()
        {
            const string countItemsProjectionName = "CountItems";
            const string countItemsProjection = @"
                fromAll().whenAny(
                    function(state, event) {
                        if (event.streamId.indexOf('bank-account-') != 0) return state;
                        if (state.count == undefined) state.count = 0;
                        state.count += 1;
                        return state;
                    });
";

            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
                store.Projections.CreatePersistent(countItemsProjectionName, countItemsProjection);

            return countItemsProjectionName;
        }

        private string CreateSumCheckForBankAccount0()
        {
            const string countItemsProjectionName = "CheckSumsInAccounts";
            const string countItemsProjection = @"
                fromAll().whenAny(
                    function(state, event) {
                        if (event.streamId.indexOf('bank-account-0') != 0) return state;
                        if (event.eventType.indexOf('AccountCredited') == 0)
                        {
                            if (state.credited == undefined) { state.credited = 0; state.credsum = '' };
                            state.credited += event.body.creditedAmount;
                            // state.credsum += '#' + event.sequenceNumber + ':' + event.body.creditedAmount + ';';
                        }

                        if (event.eventType.indexOf('AccountDebited') == 0)
                        {
                            if (state.debited == undefined) { state.debited = 0; state.debsum = '' };
                            state.debited += event.body.debitedAmount;
                            // state.debsum += '#' + event.sequenceNumber + ':' + event.body.debitedAmount + ';';
                        }

                        if (event.eventType.indexOf('AccountCheckPoint') == 0)
                        {
                            if (state.debited == undefined) return state;
                            if (state.credited == undefined) return state;
                            
                            if (state.credited != event.body.creditedAmount) throw 'Credited amount is incorrect (expected: ' + event.body.creditedAmount + ', actual: ' + state.credited + ' stream: ' + event.streamId + ' ver: ' + event.sequenceNumber + ': details: ' + state.credsum + ')'
                            if (state.debited != event.body.debitedAmount) throw 'Debited amount is incorrect (expected: ' + event.body.debitedAmount + ', actual: ' + state.debited + ' stream: ' + event.streamId + ' ver: ' + event.sequenceNumber + ': details: ' + state.debsum + ')'

                            state.success=event.sequenceNumber;
                        }
                        
                        return state;
                    });
";

            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
                store.Projections.CreatePersistent(countItemsProjectionName, countItemsProjection);

            return countItemsProjectionName;
        }
    }
}