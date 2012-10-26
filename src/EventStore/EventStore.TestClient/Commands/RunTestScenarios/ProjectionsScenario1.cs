using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class ProjectionsScenario1 : ScenarioBase
    {
        public ProjectionsScenario1(Action<IPEndPoint, byte[]> directSendOverTcp, 
                                    int maxConcurrentRequests, 
                                    int threads, 
                                    int streams, 
                                    int eventsPerStream, 
                                    int streamDeleteStep)
            : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep)
        {
        }

        protected override void RunInternal()
        {
            ThreadPool.SetMaxThreads(Connections, Connections);

            var nodeProcessId = StartNode();

            var streams = Enumerable.Range(0, Streams).Select(i => string.Format("projections-test-stream-{0}", i)).ToArray();
            var slices = Split(streams, 3);

            const string countItemsProjectionName = "CountItems";
            var store = GetConnection();

            const string countItemsProjection = @"
                fromAll().whenAny(
                    function(state, event) {
                        if (event.streamId.indexOf('projections-test-stream-') != 0) return state;
                        if (state.count == undefined) state.count = 0;
                        state.count += 1;
                        return state;
                    });
";
            store.Projections.CreateAdHoc(countItemsProjectionName, countItemsProjection);

            var w1 = Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream);
            var w2 = Write(WriteMode.Bucket, slices[1], EventsPerStream);
            var w3 = Write(WriteMode.Transactional, slices[2], EventsPerStream);

            Task.WaitAll(new [] { w1, w2, w3 });

            string state = null;
            var success = false;
            var expectedAllEventsCount = (streams.Length * EventsPerStream + streams.Length).ToString();

            var stopWatch = Stopwatch.StartNew();
            while (stopWatch.Elapsed < TimeSpan.FromMilliseconds(1000 + 10 * streams.Length * EventsPerStream))
            {
                state = store.Projections.GetState(countItemsProjectionName);
                Log.Info("Raw state: {0}", state);
                if (state.Contains(expectedAllEventsCount))
                {
                    success = true;
                    break;
                }
                Thread.Sleep(200);
            }
            
            KillNode(nodeProcessId);

            if (!success)
                throw new ApplicationException(string.Format("Count projections did not complete in time, last state: {0}",
                                                             state));
        }
    }
}