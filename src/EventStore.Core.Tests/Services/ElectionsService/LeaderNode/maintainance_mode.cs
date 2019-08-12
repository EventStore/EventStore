using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Infrastructure;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized {
	[TestFixture]
	public class elections_service_3_nodes_one_goes_into_maintainence_mode {
		private RandomizedElectionsAndGossipTestCase _randomCase;

		[SetUp]
		public void SetUp() {
			_randomCase = new RandomizedElectionsAndGossipTestCase(ElectionParams.MaxIterationCount,
				instancesCnt: 3,
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

		private MemberInfo[] CreateInitialGossip(ElectionsInstance _, ElectionsInstance[] allInstances) {
            return allInstances.Select(instance => MemberInfo.ForVNode(instance.InstanceId, DateTime.UtcNow, VNodeState.Unknown, true,
					instance.EndPoint, null, instance.EndPoint, null, instance.EndPoint, instance.EndPoint,
					-1, 0, 0, -1, -1, Guid.Empty, 0)).ToArray();
		}

		private MemberInfo[] CreateUpdatedGossip(int iteration,
			RandTestQueueItem item,
			ElectionsInstance[] instances,
			MemberInfo[] initialGossip,
			Dictionary<IPEndPoint, MemberInfo[]> previousGossip) {
                TestContext.Progress.WriteLine("Are we running?");
			if(iteration == 1){
                var previous = previousGossip[item.EndPoint];
                return previous.Select(x => {
                    if(x.InternalTcpEndPoint == item.EndPoint){
                        return x.Updated(nodePriority: int.MinValue);
                    }
                    return x;
                }).ToArray();
                    
            }

			return null;
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void should_complete_successfully([Range(0, ElectionParams.TestRunCount - 1)]
			int run) {
			var success = _randomCase.Run();
			if (!success)
				_randomCase.Logger.LogMessages();

			Console.WriteLine("There were a total of {0} messages in this run.",
				_randomCase.Logger.ProcessedItems.Count());
			Console.WriteLine("There were {0} GossipUpdated messages in this run.",
				_randomCase.Logger.ProcessedItems.Count(x => x.Message is GossipMessage.GossipUpdated));

			Assert.True(success);
		}
	}
}
