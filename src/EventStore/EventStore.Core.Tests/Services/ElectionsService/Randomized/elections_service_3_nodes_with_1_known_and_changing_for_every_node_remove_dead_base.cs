/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Private.Cluster;
using EventStore.Core.Tests.Infrastructure;
using NUnit.Framework;

namespace EventStore.Core.Private.Tests.Services.ElectionsService.Randomized
{
    internal class elections_service_3_nodes_with_1_known_and_changing_for_every_node_remove_dead_base
    {
        protected RandomizedElectionsAndGossipTestCase _randomCase;

        protected virtual int? GetRndSeed()
        {
            return null;
        }

        [SetUp]
        public void SetUp()
        {
            _randomCase = new RandomizedElectionsAndGossipTestCase(ElectionParams.MaxIterationCount,
                                                                   instancesCnt: 3,
                                                                   httpLossProbability: 0.3,
                                                                   httpDupProbability: 0.3,
                                                                   httpMaxDelay: 20,
                                                                   timerMinDelay: 100,
                                                                   timerMaxDelay: 200,
                                                                   createInitialGossip: CreateInitialGossip,
                                                                   createUpdatedGossip: CreateUpdatedGossip,
                                                                   rndSeed: GetRndSeed()
                    );

            _randomCase.Init();
        }

        protected MemberInfo[] CreateInitialGossip(ElectionsInstance instance, ElectionsInstance[] allInstances)
        {
            return new[]
                   {
                           MemberInfo.ForVNode(instance.InstanceId, DateTime.UtcNow, VNodeState.Unknown, true,
                                               instance.EndPoint, null, instance.EndPoint, null, instance.EndPoint, instance.EndPoint,
                                               0, 0, -1, -1, Guid.Empty)
                   };
        }

        protected MemberInfo[] CreateUpdatedGossip(int iteration,
                                                   RandTestQueueItem item,
                                                   ElectionsInstance[] instances,
                                                   MemberInfo[] initialGossip,
                                                   Dictionary<IPEndPoint, MemberInfo[]> previousGossip)
        {
            MemberInfo[] newGossip = null;

            if (_randomCase.Next(100) < GossipUpdateParams.AddNodeProbabilityPercent)
                newGossip = AddNewMemberFromInstances(item, instances, previousGossip);

            if (_randomCase.Next(100) < GossipUpdateParams.KillNodeProbabilityPercent 
                && (iteration < ElectionParams.MaxIterationCount - 10000))
            {
                newGossip = RemoveNotSelf(item, previousGossip);
            }

            return newGossip;
        }

        protected static MemberInfo[] AddNewMemberFromInstances(RandTestQueueItem item,
                                                                IEnumerable<ElectionsInstance> instances,
                                                                IDictionary<IPEndPoint, MemberInfo[]> previousGossip)
        {
            MemberInfo[] newGossip = null;
            var previous = previousGossip[item.EndPoint];
            var toAdd = instances.Select(x => x.EndPoint).Except(previous.Where(x => x.IsAlive).Select(x => x.InternalHttpEndPoint))
                                 .ToArray();
            if (toAdd.Any())
            {
                var endPoint = toAdd.First();

                newGossip = previous.Where(x => !x.Is(endPoint)).Union(
                    new[] 
                    { 
                            MemberInfo.ForVNode(DateTime.UtcNow, VNodeState.Unknown, true,
                                                endPoint, null, endPoint, null, endPoint, endPoint,
                                                0, 0, -1, -1, Guid.Empty) 
                    }).ToArray();
            }
            return newGossip;
        }

        protected MemberInfo[] RemoveNotSelf(RandTestQueueItem item, IDictionary<IPEndPoint, MemberInfo[]> previousGossip)
        {
            MemberInfo[] newGossip = null;
            var previous = previousGossip[item.EndPoint];

            var toKillIndex = _randomCase.Next(previous.Length);
            var endPoint = previous[toKillIndex].InternalHttpEndPoint;

            if (!endPoint.Equals(item.EndPoint))
            {
                var @new = previous.Where(x => !x.Is(endPoint))
                                   .Where(x => x.IsAlive)
                                   .Union(new[]
                                          {
                                                  MemberInfo.ForVNode(DateTime.UtcNow,
                                                                      VNodeState.Unknown,
                                                                      false,
                                                                      endPoint, null, endPoint, null, endPoint, endPoint,
                                                                      0, 0, -1, -1, Guid.Empty)
                                          });

                newGossip = @new.ToArray();
            }
            return newGossip;
        }
    }
}*/

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized
{
}