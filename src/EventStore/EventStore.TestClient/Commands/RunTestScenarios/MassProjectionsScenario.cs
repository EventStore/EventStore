using System;
using System.Threading;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class MassProjectionsScenario : ProjectionsKillScenario
    {
        public MassProjectionsScenario(Action<byte[]> directSendOverTcp, int maxConcurrentRequests, int threads, int streams, int eventsPerStream, int streamDeleteStep) : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep)
        {
        }

        protected override void RunInternal()
        {
            var nodeProcessId = StartNode();
            string lastBankAccountName = null;

            while (GetIterationCode() < 100)
            {
                SetNextIterationCode();

                CreateCountItem();
                lastBankAccountName = CreateSumCheckForBankAccount0();

                Log.Info("Created {0}", lastBankAccountName);

            }

            var writeTask = WriteData();

            KillNode(nodeProcessId);

            nodeProcessId = StartNode();
            writeTask.Wait();

            Thread.Sleep(TimeSpan.FromMinutes(5));
            var success = CheckProjectionState(GetConnection(), lastBankAccountName, "success", x => x == EventsPerStream.ToString());

            if (!success)
                throw new ApplicationException("Last bank projection failed");
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