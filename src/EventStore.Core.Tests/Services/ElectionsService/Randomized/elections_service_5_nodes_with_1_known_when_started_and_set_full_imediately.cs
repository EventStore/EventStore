using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Infrastructure;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized
{
    [TestFixture]
    public class elections_service_5_nodes_with_1_known_when_started_and_set_full_imediately
    {
        private RandomizedElectionsAndGossipTestCase _randomCase;

        [SetUp]
        public void SetUp()
        {
            _randomCase = new RandomizedElectionsAndGossipTestCase(ElectionParams.MaxIterationCount,
                                                                   instancesCnt: 5,
                                                                   httpLossProbability: 0.3,
                                                                   httpDupProbability: 0.3,
                                                                   httpMaxDelay: 20,
                                                                   timerMinDelay: 100,
                                                                   timerMaxDelay: 200,
                                                                   createInitialGossip: CreateInitialGossip,
                                                                   createUpdatedGossip: CreateUpdatedGossip
                                                                   );

            _randomCase.Init();
        }

        private MemberInfo[] CreateInitialGossip(ElectionsInstance instance, ElectionsInstance[] allInstances)
        {
            return new[] 
            { 
                MemberInfo.ForVNode(instance.InstanceId, DateTime.UtcNow, VNodeState.Unknown, true, 
                            instance.EndPoint, null, instance.EndPoint, null, instance.EndPoint, instance.EndPoint, 
                            -1, 0, 0, -1, -1, Guid.Empty, 0)
            };
        }

        private MemberInfo[] CreateUpdatedGossip(int iteration,
                                                 RandTestQueueItem item,
                                                 ElectionsInstance[] instances,
                                                 MemberInfo[] initialGossip,
                                                 Dictionary<IPEndPoint, MemberInfo[]> previousGossip)
        {
            if (previousGossip[item.EndPoint].Length < 5)
            {
                Console.WriteLine("Update item: {0} : {1}", iteration, item.EndPoint.Port);
                return instances.Select((x, i) => 
                    MemberInfo.ForVNode(x.InstanceId, DateTime.UtcNow, VNodeState.Unknown, true,
                                        x.EndPoint, null, x.EndPoint, null, x.EndPoint, x.EndPoint,
                                        -1, 0, 0, -1, -1, Guid.Empty, 0)).ToArray();
            }

            return null;
        }

        [Test, Category("LongRunning"), Category("Network"), Explicit]
        public void should_complete_successfully([Range(0, ElectionParams.TestRunCount - 1)]int run)
        {
            var success = _randomCase.Run();
            if (!success)
                _randomCase.Logger.LogMessages();

            Console.WriteLine("There were a total of {0} messages in this run.", _randomCase.Logger.ProcessedItems.Count());
            Console.WriteLine("There were {0} GossipUpdated messages in this run.",
                              _randomCase.Logger.ProcessedItems.Count(x => x.Message is GossipMessage.GossipUpdated));

            Assert.True(success);
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void should_complete_successfully2([Range(0, 9)]int run)
        {
            var success = _randomCase.Run();
            if (!success)
                _randomCase.Logger.LogMessages();

            Console.WriteLine("There were a total of {0} messages in this run.", _randomCase.Logger.ProcessedItems.Count());
            Console.WriteLine("There were {0} GossipUpdated messages in this run.",
                              _randomCase.Logger.ProcessedItems.Count(x => x.Message is GossipMessage.GossipUpdated));

            Assert.True(success);
        }
    }
}