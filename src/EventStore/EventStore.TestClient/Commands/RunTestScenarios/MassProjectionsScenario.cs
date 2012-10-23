using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class MassProjectionsScenario : ProjectionsKillScenario
    {
        public MassProjectionsScenario(Action<byte[]> directSendOverTcp, int maxConcurrentRequests, int threads, int streams, int eventsPerStream, int streamDeleteStep) : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep)
        {
        }

        protected override void RunInternal()
        {
            var success = true;

            var nodeProcessId = StartNode();

            var countProjections = new List<string>();
            var bankProjections = new List<string>();

            while (GetIterationCode() < Streams / 3)
            {
                SetNextIterationCode();

                countProjections.Add(CreateCountItem());
                bankProjections.Add(CreateSumCheckForBankAccount0());

                Log.Info("Created {0} and {1}", bankProjections[bankProjections.Count - 1], 
                                                countProjections[countProjections.Count - 1]);
            }

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            var writeTask = WriteData();

            int count = 10;
            while (count > 0)
            {
                Log.Info("Stop and start projection, remaining iterations {0}, waiting for data to be written.", count);

                var t1 = StartOrStopProjection(countProjections, false);
                var t2 = StartOrStopProjection(bankProjections, false);

                Thread.Sleep(TimeSpan.FromSeconds(20));

                var t3 = StartOrStopProjection(countProjections, true);
                var t4 = StartOrStopProjection(bankProjections, true);

                Thread.Sleep(TimeSpan.FromSeconds(Streams * EventsPerStream / 5000.0));
                Task.WaitAll(new[] { t1, t2, t3, t4 });

                if (writeTask.IsCompleted)
                    count -= 1;

                if (writeTask.IsFaulted)
                    throw new ApplicationException("Failed to write data", writeTask.Exception);

                success = CheckProjectionState(GetConnection(), 
                                                        bankProjections[bankProjections.Count - 1], 
                                                        "success", 
                                                        x => x == EventsPerStream.ToString());
                if (success)
                    break;
            }

            writeTask.Wait();

            if (!success)
            {
                var sleepTimeSeconds = (Streams * EventsPerStream) / 500;
                Log.Info("Sleep for {0} seconds", sleepTimeSeconds);
                Thread.Sleep(TimeSpan.FromSeconds(sleepTimeSeconds));
            }

            success = CheckProjectionState(GetConnection(), bankProjections[bankProjections.Count - 1], "success", x => x == EventsPerStream.ToString());

            if (!success)
                throw new ApplicationException("Last bank projection failed");
        }

        private Task StartOrStopProjection(IEnumerable<string> projections, bool enable)
        {
            var tasks = new List<Task>();
            var store = GetConnection();
            foreach (string projection in projections)
            {
                //var isRunning = store.Projections.GetStatus(projection) == "Enabled";
                tasks.Add(enable
                              ? store.Projections.EnableAsync(projection)
                              : store.Projections.DisableAsync(projection));


            }

            var task = Task.Factory.ContinueWhenAll(tasks.ToArray(), ts => { Task.WaitAll(ts); Log.Info("Projections enable/disable finished."); });
            return task;
        }

        private int _iterationCode;
        protected override int GetIterationCode()
        {
            return _iterationCode;
        }

        private void SetNextIterationCode()
        {
            _iterationCode += 1;
        }

        protected override TimeSpan IterationSleepInterval
        {
            get { return TimeSpan.FromMinutes(1); }
        }
    }
}