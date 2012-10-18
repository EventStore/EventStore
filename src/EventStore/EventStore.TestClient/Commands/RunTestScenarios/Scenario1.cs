using System;
using System.Linq;
using System.Threading;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class Scenario1 : ScenarioBase
    {
        public Scenario1(Action<byte[]> directSendOverTcp, int maxConcurrentRequests, int threads, int streams, int eventsPerStream, int streamDeleteStep)
            : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep)
        {
        }

        public override void Run()
        {
            ThreadPool.SetMaxThreads(Threads, Threads);

            var nodeProcessId = StartNode();

            var streams = Enumerable.Range(0, Streams).Select(i => string.Format("test-stream-{0}", i)).ToArray();
            var slices = Split(streams, 3);

            Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream);
            Write(WriteMode.Bucket, slices[1], EventsPerStream);
            Write(WriteMode.Transactional, slices[2], EventsPerStream);

            var deleted = streams.Where((s, i) => i % StreamDeleteStep == 0).ToArray();
            DeleteStreams(deleted);

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            CheckStreamsDeleted(deleted);

            var exceptDeleted = streams.Except(deleted).ToArray();
            Read(exceptDeleted, @from: 0, count: Piece + 1);
            Read(exceptDeleted, @from: EventsPerStream - Piece, count: Piece + 1);
            Read(exceptDeleted, @from: EventsPerStream / 2, count: Math.Min(Piece + 1, EventsPerStream - EventsPerStream / 2));

            KillNode(nodeProcessId);
        }
    }
}