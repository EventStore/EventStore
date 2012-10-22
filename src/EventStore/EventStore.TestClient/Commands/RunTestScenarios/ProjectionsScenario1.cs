using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class ProjectionsScenario1 : ScenarioBase
    {
        public ProjectionsScenario1(Action<byte[]> directSendOverTcp, 
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

            Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream);
            Write(WriteMode.Bucket, slices[1], EventsPerStream);
            Write(WriteMode.Transactional, slices[2], EventsPerStream);

            string state = null;
            var success = false;
            var expectedAllEventsCount = (streams.Length * EventsPerStream + streams.Length).ToString();

            var stopWatch = Stopwatch.StartNew();
            while (stopWatch.Elapsed < TimeSpan.FromMilliseconds(1000 + streams.Length * EventsPerStream))
            {
                var stopWatch = Stopwatch.StartNew();
                while (stopWatch.Elapsed < TimeSpan.FromMilliseconds(10000 + streams.Length * EventsPerStream))
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