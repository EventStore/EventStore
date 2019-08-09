using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.ServiceModel.Configuration;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;
using SUT = EventStore.Core.Services.ElectionsService;
namespace EventStore.Core.Tests.Services.ElectionsService {
	public class ElectionsServiceUnitTests :
		IHandle<HttpMessage.SendOverHttp> {
		private Dictionary<IPEndPoint, IPublisher> _nodes;
		private List<MemberInfo> _members;
		private FakeTimeProvider _fakeTimeProvider;
		private FakeScheduler _scheduler;


		[SetUp]
		public void Setup() {
			var address = IPAddress.Loopback;
			var members = new List<MemberInfo>();
			var seeds = new List<IPEndPoint>();
			var seedSource = new ReallyNotSafeFakeGossipSeedSource(seeds);
			_nodes = new Dictionary<IPEndPoint, IPublisher>();
			for (int i = 0; i < 3; i++) {
				var inputBus = new InMemoryBus($"ELECTIONS-INPUT-BUS-NODE-{i}", watchSlowMsg: false);
				var outputBus = new InMemoryBus($"ELECTIONS-OUTPUT-BUS-NODE-{i}", watchSlowMsg: false);
				var endPoint = new IPEndPoint(address, 1000 + i);
				seeds.Add(endPoint);
				var instanceId = Guid.Parse($"101EFD13-F9CD-49BE-9C6D-E6AF9AF5540{i}");
				members.Add(MemberInfo.ForVNode(instanceId, DateTime.UtcNow, VNodeState.Unknown, true,
					endPoint, null, endPoint, null, endPoint, endPoint, -1, 0, 0, -1, -1, Guid.Empty, 0)
				);
				var nodeInfo = new VNodeInfo(instanceId, 0, endPoint, endPoint, endPoint, endPoint, endPoint,
					endPoint);
				_fakeTimeProvider = new FakeTimeProvider();
				_scheduler = new FakeScheduler(new FakeTimer(), _fakeTimeProvider);
				var timerService = new TimerService(_scheduler);

				var writerCheckpoint = new InMemoryCheckpoint();
				var readerCheckpoint = new InMemoryCheckpoint();
				var epochManager = new FakeEpochManager();
				Func<long> lastCommitPosition = () => -1;
				var electionsService = new Core.Services.ElectionsService(outputBus,
					nodeInfo,
					3,
					writerCheckpoint,
					readerCheckpoint,
					epochManager,
					() => -1, 0);
				electionsService.SubscribeMessages(inputBus);

				outputBus.Subscribe<HttpMessage.SendOverHttp>(this);
				var nodeId = i;
				outputBus.Subscribe(new AdHocHandler<Message>(
					m => {
						switch (m) {
							case TimerMessage.Schedule sm:
								TestContext.WriteLine($"Node {nodeId} : Delay {sm.TriggerAfter} : {sm.ReplyMessage.GetType()}");
								timerService.Handle(sm);
								break;
							case HttpMessage.SendOverHttp hm:
								TestContext.WriteLine($"Node {nodeId} : EP {hm.EndPoint} : {hm.Message.GetType()}");
								break;
							default:
								TestContext.WriteLine($"Node {nodeId} : EP {m.GetType()}");
								inputBus.Publish(m);
								break;
						}
					}
					));
				_nodes.Add(endPoint, inputBus);

				var gossip = new NodeGossipService(outputBus, seedSource, nodeInfo, writerCheckpoint, readerCheckpoint,
					epochManager, lastCommitPosition, 0, TimeSpan.FromMilliseconds(500), TimeSpan.FromDays(1));
				inputBus.Subscribe<SystemMessage.SystemInit>(gossip);
				inputBus.Subscribe<GossipMessage.RetrieveGossipSeedSources>(gossip);
				inputBus.Subscribe<GossipMessage.GotGossipSeedSources>(gossip);
				inputBus.Subscribe<GossipMessage.Gossip>(gossip);
				inputBus.Subscribe<GossipMessage.GossipReceived>(gossip);
				inputBus.Subscribe<SystemMessage.StateChangeMessage>(gossip);
				inputBus.Subscribe<GossipMessage.GossipSendFailed>(gossip);
				inputBus.Subscribe<GossipMessage.UpdateNodePriority>(gossip);
				inputBus.Subscribe<SystemMessage.VNodeConnectionEstablished>(gossip);
				inputBus.Subscribe<SystemMessage.VNodeConnectionLost>(gossip);
			}

			_members = members;
		}

		[Test]
		public void Foo() {

			foreach (var node in _nodes.Reverse()) {
				node.Value.Publish(new SystemMessage.SystemInit());
				node.Value.Publish(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
			}
			_fakeTimeProvider.AddTime(TimeSpan.FromMilliseconds(250));
			_scheduler.TriggerProcessing();
		}


		public void Handle(HttpMessage.SendOverHttp message) {
			if (_nodes.TryGetValue(message.EndPoint, out var node)) {
				TestContext.WriteLine($"Sending {message.Message} to {message.EndPoint}");
				node.Publish(message.Message);
				return;
			}
			TestContext.WriteLine($"Failed to find endpoint for {message.Message} to {message.EndPoint}");
		}


		class ReallyNotSafeFakeGossipSeedSource : IGossipSeedSource {
			private readonly List<IPEndPoint> _ipEndPoints;

			public ReallyNotSafeFakeGossipSeedSource(List<IPEndPoint> ipEndPoints) {
				_ipEndPoints = ipEndPoints;
			}

			public IAsyncResult BeginGetHostEndpoints(AsyncCallback requestCallback, object state) {
				requestCallback(null);
				return null;
			}

			public IPEndPoint[] EndGetHostEndpoints(IAsyncResult asyncResult) {
				return _ipEndPoints.ToArray();
			}
		}


	}

	public class ChoosingMasterTests {

		static IEnumerable<TestCase> CreateCases() {
            //Basic cases
            yield return new TestCase(2, 0);

            yield return new TestCase(0, 0, 
	            nodePriorities: new int[]{3, 2, 1});

            //maintenance checks
            yield return new TestCase(1, 0,
	            nodePriorities: new[] {0, 0, int.MinValue});

            //Node behind
            yield return new TestCase(0, 0, 
	            commitPositions: new long[] {1, 0, 1},
	            nodePriorities: new[] {0, 0, int.MinValue});
            yield return new TestCase(0, 0,
	            writerCheckpoints: new long[] {1, 0, 1}, 
	            nodePriorities: new[] {0, 0, int.MinValue});
            yield return new TestCase(0, 0,
	            chaserCheckpoints: new long[] {1, 0, 1}, 
	            nodePriorities: new[] {0, 0, int.MinValue});

            //2 nodes in maintenance

            yield return new TestCase(0, 0,
	            nodePriorities: new[] {0, int.MinValue, int.MinValue});

            yield return new TestCase(1, 0,
	            nodePriorities: new[] {int.MinValue,0,  int.MinValue});

            yield return new TestCase(2, 0,
	            nodePriorities: new[] {int.MinValue, int.MinValue, 0});

            //and non maintenance mode behind
            yield return new TestCase(2, 0,
	            commitPositions: new long[] {1, 0, 1},
	            nodePriorities: new[] {int.MinValue, 0,  int.MinValue});
            
            yield return new TestCase(2, 0,
	            writerCheckpoints: new long[] {1, 0, 1},
	            nodePriorities: new[] {int.MinValue, 0,  int.MinValue});
            yield return new TestCase(2, 0,
	            chaserCheckpoints: new long[] {1, 0, 1}, 
	            nodePriorities: new[] {int.MinValue, 0,  int.MinValue});

			// all 3 nodes on maintenance
			yield return new TestCase(0, 0,
	            nodePriorities: new[] {int.MinValue, int.MinValue, int.MinValue},
				lastElectedMaster: 0);
		}

		[Test, TestCaseSource(nameof(TestCases))]
		public void ShouldSelectValidBestMasterCandidate(TestCase tc) {
			var epochId = Guid.NewGuid();
			var members = new MemberInfo[3];
			var prepareOks = new Dictionary<Guid, ElectionMessage.PrepareOk>();
			Func<int, long> lastCommitPosition = i => tc.CommitPositions[i];
			Func<int, long> writerCheckpoint = i => tc.WriterCheckpoints[i];
			Func<int, long> chaserCheckpoint = i => tc.ChaserCheckpoints[i];
			Func<int, int> nodePriority = i => tc.NodePriorities[i];

			for (int index = 0; index < 3; index++) {
				TestContext.Progress.WriteLine("");
				members[index] = CreateMemberInfo(index, epochId, lastCommitPosition, writerCheckpoint, chaserCheckpoint, nodePriority);
				var pok = CreatePrepareOk(index, epochId, lastCommitPosition, writerCheckpoint, chaserCheckpoint, nodePriority);
				prepareOks.Add(pok.ServerId, pok);
			}

			var mc = SUT.GetBestMasterCandidate(prepareOks);

			Assert.AreEqual(IdForNode(tc.ExpectedMasterCandidateNode), mc.InstanceId);

			var ownInfo = CreateMasterCandidate(1, epochId, lastCommitPosition, writerCheckpoint, chaserCheckpoint, nodePriority);

			var localNode = FromMember(0, members);

			var isLegit = SUT.IsLegitimateMaster(1, EndpointForNode(tc.ProposingNode),
				IdForNode(tc.ProposingNode), mc, members, null, 0, localNode,
				ownInfo);

			Assert.True(isLegit);
		}


		static ElectionMessage.PrepareOk CreatePrepareOk(int i, Guid epochId,
			Func<int, long> lastCommitPosition,
			Func<int, long> writerCheckpoint,
			Func<int, long> chaserCheckpoint,
			Func<int, int> nodePriority) {
			var id = IdForNode(i);
			var ep = EndpointForNode(i);
			return new ElectionMessage.PrepareOk(1, id, ep, 1, 1, epochId, lastCommitPosition(i), writerCheckpoint(i), chaserCheckpoint(i), nodePriority(i));
		}

		static SUT.MasterCandidate CreateMasterCandidate(int i, Guid epochId,
			Func<int, long> lastCommitPosition,
			Func<int, long> writerCheckpoint,
			Func<int, long> chaserCheckpoint,
			Func<int, int> nodePriority) {
			var id = IdForNode(i);
			var ep = EndpointForNode(i);
			return new SUT.MasterCandidate(id, ep, 1, 1, epochId, lastCommitPosition(i), writerCheckpoint(i), chaserCheckpoint(i), nodePriority(i));
		}

		static VNodeInfo FromMember(int index, MemberInfo[] members) {
			return new VNodeInfo(members[index].InstanceId, 1, members[index].InternalTcpEndPoint, members[index].InternalSecureTcpEndPoint, members[index].ExternalTcpEndPoint, members[index].ExternalSecureTcpEndPoint, members[index].InternalHttpEndPoint, members[index].ExternalHttpEndPoint);
		}

		static MemberInfo CreateMemberInfo(int i, Guid epochId, Func<int, long> lastCommitPosition,
			Func<int, long> writerCheckpoint,
			Func<int, long> chaserCheckpoint,
			Func<int, int> nodePriority) {
			var id = IdForNode(i);
			var ep = EndpointForNode(i);
			return MemberInfo.ForVNode(id, DateTime.Now, VNodeState.Slave, true, ep, ep, ep, ep, ep, ep, lastCommitPosition(i), writerCheckpoint(i), chaserCheckpoint(i), 1, 1, epochId, nodePriority(i));
		}

		private static IPEndPoint EndpointForNode(int i) {
			return new IPEndPoint(IPAddress.Loopback, 1000 + i);
		}

		private static Guid IdForNode(int i) {
			return Guid.Parse($"101EFD13-F9CD-49BE-9C6D-E6AF9AF5540{i}");
		}

		static object[] TestCases() {
			return CreateCases().Cast<object>().ToArray();
		}

        public class TestCase {
			private readonly string _name;
			public int ExpectedMasterCandidateNode { get; }
			public int ProposingNode { get; }
			public int? LastElectedMaster { get; }
			public long[] CommitPositions { get; }
			public long[] WriterCheckpoints { get; }
			public long[] ChaserCheckpoints { get; }
			public int[] NodePriorities { get; }

			public TestCase(int expectedMasterCandidateNode, int proposingNode,
				long[] commitPositions = null,
				long[] writerCheckpoints = null,
				long[] chaserCheckpoints = null,
				int[] nodePriorities = null,
				int? lastElectedMaster = null) {
				_name = GenerateName(expectedMasterCandidateNode, commitPositions, writerCheckpoints, chaserCheckpoints, nodePriorities);
				ExpectedMasterCandidateNode = expectedMasterCandidateNode;
				ProposingNode = proposingNode;
				CommitPositions = commitPositions ?? new[] { 1L, 1, 1 };
				WriterCheckpoints = writerCheckpoints ?? new[] { 1L, 1, 1 };
				ChaserCheckpoints = chaserCheckpoints ?? new[] { 1L, 1, 1 };
				NodePriorities = nodePriorities ??  new []{1,1,1};
				LastElectedMaster = lastElectedMaster;
			}

			private static string GenerateName(int expectedMasterCandidateNode, long[] commitPositions, long[] writerCheckpoints,
				long[] chaserCheckpoints, int[] nodePriorities)
			{
				var nameBuilder = new StringBuilder();
				if (commitPositions != null)
				{
					if (nameBuilder.Length == 0) nameBuilder.Append("Nodes with ");
					else nameBuilder.Append(" and");
					nameBuilder.AppendFormat("commit positions ( {0} )",string.Join(",",
						commitPositions.Where(x => x != 1).Select((x, i) => $"{i} : cp {x}")));
				}

				if (writerCheckpoints != null)
				{
					if (nameBuilder.Length == 0) nameBuilder.Append("Nodes with ");
					else nameBuilder.Append(" and ");
					nameBuilder.AppendFormat("writer checkpoints ( {0} )",string.Join(",",
						writerCheckpoints.Where(x => x != 1).Select((x, i) => $"{i} : wcp {x}")));
				}

				if (chaserCheckpoints != null)
				{
					if (nameBuilder.Length == 0) nameBuilder.Append("Nodes with ");
					else nameBuilder.Append(" and ");
					nameBuilder.AppendFormat("chaser checkpoints ( {0} )",string.Join(",",
						chaserCheckpoints.Where(x => x != 1).Select((x, i) => $"{i} : ccp {x}")));
				}

				if (nodePriorities != null)
				{
					if (nameBuilder.Length == 0) nameBuilder.Append("Nodes with ");
					else nameBuilder.Append(" and ");
					nameBuilder.AppendFormat("node priorities ( {0} )",string.Join(",",
						nodePriorities.Where(x => x != 0).Select((x, i) => $"{i} : np {x}")));
				}

				if (nameBuilder.Length == 0)
					nameBuilder.AppendFormat("All nodes caught up with the same priority expect {0} to be master",
						expectedMasterCandidateNode);
				var name = nameBuilder.ToString();
				return name;
			}

			public override string ToString() {
				return _name;
			}
		}
	}
}
