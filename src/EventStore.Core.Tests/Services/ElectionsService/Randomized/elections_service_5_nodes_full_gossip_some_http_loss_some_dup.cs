using System;
using System.Linq;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized
{
    [TestFixture]
    public class elections_service_5_nodes_full_gossip_some_http_loss_some_dup
    {
        private RandomizedElectionsTestCase _randomCase;

        [SetUp]
        public void SetUp()
        {
            _randomCase = new RandomizedElectionsTestCase(ElectionParams.MaxIterationCount,
                                                          instancesCnt: 5,
                                                          httpLossProbability: 0.3,
                                                          httpDupProbability: 0.3,
                                                          httpMaxDelay: 20,
                                                          timerMinDelay: 100,
                                                          timerMaxDelay: 200);
            _randomCase.Init();
        }

        [Test, Category("LongRunning"), Category("Network"), Explicit]
        public void should_always_arrive_at_coherent_results([Range(0, ElectionParams.TestRunCount - 1)]int run)
        {
            var success = _randomCase.Run();
            if (!success)
                _randomCase.Logger.LogMessages();
            Console.WriteLine("There were a total of {0} messages in this run.", _randomCase.Logger.ProcessedItems.Count());
            Assert.True(success);
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void should_always_arrive_at_coherent_results2([Range(0, 9)]int run)
        {
            var success = _randomCase.Run();
            if (!success)
                _randomCase.Logger.LogMessages();
            Console.WriteLine("There were a total of {0} messages in this run.", _randomCase.Logger.ProcessedItems.Count());
            Assert.True(success);
        }
    }
}
