/*using System;
using System.Linq;
using EventStore.Core.Private.Messages;
using NUnit.Framework;

namespace EventStore.Core.Private.Tests.Services.ElectionsService.Randomized
{
    [TestFixture, Ignore("Fails on ubuntu with rndSeed 1657671789")]
    internal class elections_service_3_nodes_with_1_known_and_changing_for_every_node_remove_dead :
            elections_service_3_nodes_with_1_known_and_changing_for_every_node_remove_dead_base
    {
        [Test, Category("LongRunning"), Explicit]
        [Category("Network")]
        public void should_complete_successfully([Range(100, 100 + ElectionParams.TestRunCount - 1)]int run)
        {
            var success = _randomCase.Run();
            if (!success)
            {
                _randomCase.Logger.LogMessages();
                _randomCase.FinishCondition.Log();
            }
            
            Console.WriteLine("There were total {0} messages in this run.", _randomCase.Logger.ProcessedItems.Count());
            Console.WriteLine("There were {0} GossipUpdated messages in this run.",
                              _randomCase.Logger.ProcessedItems.Count(x => x.Message is GossipMessage.GossipUpdated));

            Assert.True(success);
        }
    }
}*/

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized
{
}