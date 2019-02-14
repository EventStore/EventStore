using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Tests.Services.ElectionsService {
	public class ElectionsServiceUnit {
		private const int LastCommitPosition = -1;
		private const int WriterCheckpoint = 0;
		private const int ChaserCheckpoint = 0;
		private static readonly DateTime InitialDate = new DateTime(2012, 6, 1);

		public ClusterInfo ClusterInfo { get; private set; }

		public IPEndPoint OwnEndPoint {
			get { return InitialClusterSettings.Self.NodeInfo.InternalHttp; }
		}

		protected Core.Services.ElectionsService ElectionsService;

		public readonly FakePublisher Publisher;
		public readonly List<Message> InputMessages;

		private readonly InMemoryBus _bus;

		protected readonly ClusterSettings InitialClusterSettings;
		protected readonly ClusterInfo InitialClusterInfo;

		public ElectionsServiceUnit(ClusterSettings clusterSettings) {
			Publisher = new FakePublisher();

			_bus = new InMemoryBus(GetType().Name);
			ElectionsService = new Core.Services.ElectionsService(Publisher,
				clusterSettings.Self.NodeInfo,
				clusterSettings.ClusterNodesCount,
				new InMemoryCheckpoint(WriterCheckpoint),
				new InMemoryCheckpoint(ChaserCheckpoint),
				new FakeEpochManager(),
				() => -1, 0);
			ElectionsService.SubscribeMessages(_bus);

			InputMessages = new List<Message>();

			InitialClusterSettings = clusterSettings;
			InitialClusterInfo = BuildClusterInfo(clusterSettings);
			ClusterInfo = new ClusterInfo(InitialClusterInfo.Members);
		}

		private ClusterInfo BuildClusterInfo(ClusterSettings clusterSettings) {
			var members =
				(new[] {
					MemberInfo.ForManager(Guid.Empty, InitialDate, true, clusterSettings.ClusterManager,
						clusterSettings.ClusterManager)
				})
				.Union(new[] {
					MemberInfo.ForVNode(clusterSettings.Self.NodeInfo.InstanceId,
						InitialDate,
						VNodeState.Unknown,
						true,
						clusterSettings.Self.NodeInfo.InternalTcp,
						clusterSettings.Self.NodeInfo.InternalSecureTcp,
						clusterSettings.Self.NodeInfo.ExternalTcp,
						clusterSettings.Self.NodeInfo.ExternalSecureTcp,
						clusterSettings.Self.NodeInfo.InternalHttp,
						clusterSettings.Self.NodeInfo.ExternalHttp,
						LastCommitPosition, WriterCheckpoint, ChaserCheckpoint,
						-1,
						-1,
						Guid.Empty, 0)
				})
				.Union(clusterSettings.GroupMembers
					.Select(x => MemberInfo.ForVNode(x.NodeInfo.InstanceId,
						InitialDate,
						VNodeState.Unknown,
						true,
						x.NodeInfo.InternalTcp,
						x.NodeInfo.InternalSecureTcp,
						x.NodeInfo.ExternalTcp,
						x.NodeInfo.ExternalSecureTcp,
						x.NodeInfo.InternalHttp,
						x.NodeInfo.ExternalHttp,
						LastCommitPosition, WriterCheckpoint, ChaserCheckpoint,
						-1,
						-1,
						Guid.Empty, 0)));

			var ordered = members.OrderBy(x =>
				string.Format("{0}:{1}", x.InternalHttpEndPoint.ToString(), x.InternalHttpEndPoint.Port));

			return new ClusterInfo(ordered.ToArray());
		}

		public void Publish(Message message) {
			InputMessages.Add(message);
			_bus.Publish(message);
		}

		public void Publish(IEnumerable<Message> messages) {
			foreach (var message in messages) {
				Publish(message);
			}
		}

		public T[] ClearMessageFromQueue<T>() {
			return ClearMessageFromQueue(x => (x is T)).Cast<T>().ToArray();
		}

		public Message[] ClearMessageFromQueue(Func<Message, bool> predicate) {
			var removedList = new List<Message>();
			var removedCount = 0;
			var index = 0;
			foreach (var message in Publisher.Messages.ToList()) {
				if (predicate(message)) {
					removedList.Add(message);
					Publisher.Messages.RemoveAt(index - removedCount);

					removedCount += 1;
				}

				index += 1;
			}

			return removedList.ToArray();
		}

		public Message[] RepublishFromPublisher(bool skipScheduledMessages = false) {
			var httpAndOtherMessages = Publisher.Messages.ToLookup(x => (x is HttpMessage.SendOverHttp));
			var httpMessages = httpAndOtherMessages[true].ToList();
			var messages = httpAndOtherMessages[false].ToList();
			Publisher.Messages.Clear();

			messages.Where(x => !(x is TimerMessage.Schedule)).ToList()
				.ForEach(x => {
					messages.Remove(x);
					Publish(x);
				});

			if (skipScheduledMessages == false) {
				messages.OfType<TimerMessage.Schedule>().ToList()
					.ForEach(x => {
						messages.Remove(x);
						x.Reply();
					});
			}

			var notConsumed = (messages.Concat(httpMessages)).ToArray();
			return notConsumed;
		}

		public bool IsCurrent(IPEndPoint endPoint) {
			return InitialClusterSettings.Self.NodeInfo.Is(endPoint);
		}

		public MemberInfo GetNodeAt(int index) {
			return InitialClusterInfo.Members.Where(x => x.State != VNodeState.Manager).ElementAt(index);
		}

		public IEnumerable<MemberInfo> ListMembers(Func<MemberInfo, bool> predicate = null) {
			predicate = predicate ?? (x => true);
			return ClusterInfo.Members.Where(predicate).Select(x =>
				x.State == VNodeState.Manager
					? MemberInfo.ForManager(x.InstanceId, x.TimeStamp, x.IsAlive, x.InternalHttpEndPoint,
						x.ExternalHttpEndPoint)
					: MemberInfo.ForVNode(x.InstanceId, x.TimeStamp, x.State, x.IsAlive,
						x.InternalTcpEndPoint, x.InternalSecureTcpEndPoint,
						x.ExternalTcpEndPoint, x.ExternalSecureTcpEndPoint,
						x.InternalHttpEndPoint, x.ExternalHttpEndPoint,
						x.LastCommitPosition, x.WriterCheckpoint, x.ChaserCheckpoint,
						x.EpochPosition, x.EpochNumber, x.EpochId, x.NodePriority));
		}

		public IEnumerable<MemberInfo> ListAliveMembers(Func<MemberInfo, bool> predicate = null) {
			return ListMembers(predicate).Where(x => x.IsAlive);
		}

		public void UpdateClusterMemberInfo(int nodeIndex,
			VNodeState? role = null,
			bool? isAlive = null,
			long? writerCheckpoint = null,
			long? chaserCheckpoint = null) {
			ClusterInfo.Members[nodeIndex] = ClusterInfo.Members[nodeIndex].Updated(state: role,
				isAlive: isAlive,
				writerCheckpoint: writerCheckpoint,
				chaserCheckpoint: chaserCheckpoint);
		}
	}
}
