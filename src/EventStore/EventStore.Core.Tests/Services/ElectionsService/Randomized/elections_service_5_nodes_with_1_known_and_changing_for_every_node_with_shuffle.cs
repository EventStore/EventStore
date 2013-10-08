/*using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Private.Cluster;
using EventStore.Core.Private.Messages;
using EventStore.Core.Tests.Infrastructure;
using NUnit.Framework;

namespace EventStore.Core.Private.Tests.Services.ElectionsService.Randomized
{
    [TestFixture, Ignore("Unstable")]
    public class elections_service_5_nodes_with_1_known_and_changing_for_every_node_with_shuffle
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
            return new[] { MemberInfo.ForVNode(DateTime.UtcNow, 
                            VNodeState.Unknown, 
                            true, 
                            instance.EndPoint, 
                            null,
                            instance.EndPoint,
                            null,
                            instance.EndPoint, 
                            instance.EndPoint, 
                            0, 0,
                            -1, -1, Guid.Empty)
                          };
        }

        private MemberInfo[] CreateUpdatedGossip(int iteration,
                                                 RandTestQueueItem item,
                                                 ElectionsInstance[] instances,
                                                 MemberInfo[] initialGossip,
                                                 Dictionary<IPEndPoint, MemberInfo[]> previousGossip)
        {
            MemberInfo[] newGossip = null;

            if (_randomCase.Next(100) < GossipUpdateParams.AddNodeProbabilityPercent)
                newGossip = AddNewMemberFromInstances(item, instances, previousGossip);

            if (_randomCase.Next(100) < GossipUpdateParams.KillNodeProbabilityPercent && (iteration < ElectionParams.MaxIterationCount - 12000))
                newGossip = RemoveNotSelf(item, previousGossip);

            if (newGossip != null)
                newGossip = SeqHelpers.Shuffle(newGossip, _randomCase.Next).ToArray();

            return newGossip;
        }

        private static MemberInfo[] AddNewMemberFromInstances(RandTestQueueItem item,
                                                              IEnumerable<ElectionsInstance> instances,
                                                              IDictionary<IPEndPoint, MemberInfo[]> previousGossip)
        {
            MemberInfo[] newGossip = null;
            var previous = previousGossip[item.EndPoint];
            var toAdd = instances.Select(x => x.EndPoint).Except(previous.Where(x => x.IsAlive)
                                                                         .Select(x => x.InternalHttpEndPoint))
                                 .ToArray();
            if (toAdd.Any())
            {
                var toAddItem = toAdd.Take(1).FirstOrDefault();
                if (toAddItem != null)
                {
                    newGossip = previous.Where(x => !x.Is(toAddItem)).Union(new[] { 
                                                MemberInfo.ForVNode(DateTime.UtcNow,
                                                                    VNodeState.Unknown,
                                                                    true,
                                                                    toAddItem,
                                                                    null,
                                                                    toAddItem,
                                                                    null,
                                                                    toAddItem,
                                                                    toAddItem,
                                                                    0, 0, 
                                                                    -1, -1, Guid.Empty) }).ToArray();
                }
            }
            return newGossip;
        }

        private MemberInfo[] RemoveNotSelf(RandTestQueueItem item, IDictionary<IPEndPoint, MemberInfo[]> previousGossip)
        {
            MemberInfo[] newGossip = null;
            var previous = previousGossip[item.EndPoint];

            var toKillIndex = _randomCase.Next(previous.Length);
            var endPoint = previous[toKillIndex].InternalHttpEndPoint;

            if (!endPoint.Equals(item.EndPoint))
            {
                var @new = previous.Where((x, i) => i != toKillIndex).Union(new[]
                    {
                        MemberInfo.ForVNode(DateTime.UtcNow,
                                            VNodeState.Unknown,
                                            false,
                                            endPoint, null, endPoint, null, endPoint, endPoint,
                                            0, 0, 
                                            -1, -1, Guid.Empty)
                    });

                newGossip = @new.ToArray();
            }
            return newGossip;
        }

        //[Test, Category("LongRunning"), Explicit]
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