using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Infrastructure;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized {
	internal class UpdateGossipProcessor : IRandTestItemProcessor {
		private readonly SendOverHttpBlockingProcessor _sendOverHttpProcessor;
		private readonly RandomizedElectionsAndGossipTestCase.CreateUpdatedGossip _createUpdatedGossip;
		private readonly Action<RandTestQueueItem, Message> _enqueue;
		private ElectionsInstance[] _instances;
		private readonly List<RandTestQueueItem> _processedItems;

		private MemberInfo[] _initialGossip;
		private Dictionary<IPEndPoint, MemberInfo[]> _previousGossip;

		public UpdateGossipProcessor(IEnumerable<ElectionsInstance> allInstances,
			SendOverHttpBlockingProcessor sendOverHttpProcessor,
			RandomizedElectionsAndGossipTestCase.CreateUpdatedGossip createUpdatedGossip,
			Action<RandTestQueueItem, Message> enqueue) {
			_sendOverHttpProcessor = sendOverHttpProcessor;
			_createUpdatedGossip = createUpdatedGossip;
			_enqueue = enqueue;
			_instances = allInstances.ToArray();

			_processedItems = new List<RandTestQueueItem>();
			ProcessedItems = _processedItems;
		}

		public void SetInitialData(IEnumerable<ElectionsInstance> allInstances,
			IEnumerable<MemberInfo> initialGossip) {
			_instances = allInstances.ToArray();
			_initialGossip = initialGossip.ToArray();
			_previousGossip = _instances.ToDictionary(x => x.EndPoint, v => _initialGossip);
		}

		public IEnumerable<RandTestQueueItem> ProcessedItems { get; private set; }

		public void Process(int iteration, RandTestQueueItem item) {
			var electionsDone = item.Message as ElectionMessage.ElectionsDone;
			if (electionsDone != null) {
				MemberInfo[] previousMembers;
				if (_previousGossip.TryGetValue(item.EndPoint, out previousMembers)) {
					var masterMemberIndex = Array.FindIndex(previousMembers,
						x => x.Is(electionsDone.Master.InternalHttpEndPoint));
					if (masterMemberIndex != -1) {
						var previousMasterInfo = previousMembers[masterMemberIndex];
						var masterEndPoint = previousMasterInfo.InternalHttpEndPoint;

						previousMembers[masterMemberIndex] =
							MemberInfo.ForVNode(previousMasterInfo.InstanceId, DateTime.UtcNow, VNodeState.Master,
								previousMasterInfo.IsAlive,
								masterEndPoint, null, masterEndPoint, null, masterEndPoint, masterEndPoint,
								-1, 0, 0, -1, -1, Guid.Empty, 0);
					}
				}
			}

			var updatedGossip = _createUpdatedGossip(iteration, item, _instances, _initialGossip, _previousGossip);
			if (updatedGossip != null) {
				if (updatedGossip.Length > _instances.Length)
					throw new InvalidDataException(
						"Gossip should not contain more items than there are servers in the cluster.");

				_processedItems.Add(item);

				foreach (var memberInfo in updatedGossip) {
					_sendOverHttpProcessor.RegisterEndpointToSkip(memberInfo.ExternalTcpEndPoint, !memberInfo.IsAlive);
				}

				var updateGossipMessage = new GossipMessage.GossipUpdated(new ClusterInfo(updatedGossip));

				_enqueue(item, updateGossipMessage);
				_previousGossip[item.EndPoint] = updatedGossip;

				var master =
					updateGossipMessage.ClusterInfo.Members.FirstOrDefault(x =>
						x.IsAlive && x.State == VNodeState.Master);
				if (master == null)
					_enqueue(item, new ElectionMessage.StartElections());
			}
		}

		public void LogMessages() {
		}
	}
}
