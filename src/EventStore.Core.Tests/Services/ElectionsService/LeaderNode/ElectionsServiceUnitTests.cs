using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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
					endPoint, null, endPoint, null, endPoint, endPoint, -1, 0, 0, -1, -1, Guid.Empty, 0, false)
				);
				var nodeInfo = new VNodeInfo(instanceId, 0, endPoint, endPoint, endPoint, endPoint, endPoint,
					endPoint, false);
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
					() => -1, 0, new FakeTimeProvider());
				electionsService.SubscribeMessages(inputBus);

				outputBus.Subscribe<HttpMessage.SendOverHttp>(this);
				var nodeId = i;
				outputBus.Subscribe(new AdHocHandler<Message>(
					m => {
						switch (m) {
							case TimerMessage.Schedule sm:
								TestContext.WriteLine(
									$"Node {nodeId} : Delay {sm.TriggerAfter} : {sm.ReplyMessage.GetType()}");
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
			yield return new TestCase {
				ExpectedMasterCandidateNode = 2,
				ProposingNode = 0,
			};

			yield return new TestCase {
				ExpectedMasterCandidateNode = 0,
				NodePriorities = new[] {3, 2, 1}
			};

			yield return new TestCase {
				ExpectedMasterCandidateNode = 1,
				NodePriorities = new[] {0, 0, int.MinValue},
				LastElectedMaster = 2,
				ResigningMaster = 2
			};

			yield return new TestCase {
				ExpectedMasterCandidateNode = 0,
				NodePriorities = new[] {int.MinValue, int.MinValue, int.MinValue},
				LastElectedMaster = 0,
			};

			yield return new TestCase {
				ExpectedMasterCandidateNode = 0,
				CommitPositions = new long[] {1, 0, 1},
				NodePriorities = new[] {0, 0, int.MinValue},
				LastElectedMaster = 2,
				ResigningMaster = 2
			};
			
			yield return new TestCase {
				ExpectedMasterCandidateNode = 1,
				WriterCheckpoints = new long[] {0, 1, 1},
				NodePriorities = new[] {0, 0, int.MinValue},
				LastElectedMaster = 2,
				ResigningMaster = 2
			};
			
			yield return new TestCase {
				ExpectedMasterCandidateNode = 0,
				ChaserCheckpoints = new long[] {1, 0, 1},
				NodePriorities = new[] {0, 0, int.MinValue},
				LastElectedMaster = 2,
				ResigningMaster = 2
			};

			yield return new TestCase {
				ExpectedMasterCandidateNode = 0,
				CommitPositions = new long[] {1, 0, 0},
				NodePriorities = new[] {int.MinValue, 0, 0},
				LastElectedMaster = 0
			};
			
			yield return new TestCase {
				ExpectedMasterCandidateNode = 1,
				WriterCheckpoints = new long[] {0, 1, 0},
				NodePriorities = new[] {0, int.MinValue, 0},
				LastElectedMaster = 1
			};
			
			yield return new TestCase {
				ExpectedMasterCandidateNode = 2,
				ChaserCheckpoints = new long[] {0, 0, 1},
				NodePriorities = new[] {0, 0, int.MinValue},
				LastElectedMaster = 2
			};

			yield return new TestCase {
				ExpectedMasterCandidateNode = 0,
				NodePriorities = new[] {0, int.MinValue, int.MinValue},
				LastElectedMaster = 1,
				ResigningMaster = 1
			};

			yield return new TestCase {
				ExpectedMasterCandidateNode = 1,
				NodePriorities = new[] {int.MinValue, 0, int.MinValue},
				LastElectedMaster = 0,
				ResigningMaster = 0
			};

			yield return new TestCase {
				ExpectedMasterCandidateNode = 2,
				NodePriorities = new[] {int.MinValue, int.MinValue, 0},
				LastElectedMaster = 1,
				ResigningMaster = 1
			};

			yield return new TestCase {
				ExpectedMasterCandidateNode = 2,
				CommitPositions = new long[] {1, 0, 1},
				NodePriorities = new[] {int.MinValue, 0, int.MinValue},
				LastElectedMaster = 0,
				ResigningMaster = 0
			};
			
			yield return new TestCase {
				ExpectedMasterCandidateNode = 2,
				WriterCheckpoints = new long[] {1, 0, 1},
				NodePriorities = new[] {int.MinValue, 0, int.MinValue},
				LastElectedMaster = 0,
				ResigningMaster = 0
			};
			
			yield return new TestCase {
				ExpectedMasterCandidateNode = 2,
				ChaserCheckpoints = new long[] {1, 0, 1},
				NodePriorities = new[] {int.MinValue, 0, int.MinValue},
				LastElectedMaster = 0,
				ResigningMaster = 0
			};
		}

		[Test, TestCaseSource(nameof(TestCases))]
		public void should_select_valid_best_master_candidate(TestCase tc) {
			var epochId = Guid.NewGuid();
			var members = new MemberInfo[3];
			var prepareOks = new Dictionary<Guid, ElectionMessage.PrepareOk>();
			Func<int, long> lastCommitPosition = i => tc.CommitPositions[i];
			Func<int, long> writerCheckpoint = i => tc.WriterCheckpoints[i];
			Func<int, long> chaserCheckpoint = i => tc.ChaserCheckpoints[i];
			Func<int, int> nodePriority = i => tc.NodePriorities[i];

			for (int index = 0; index < 3; index++) {
				members[index] = CreateMemberInfo(index, epochId, lastCommitPosition, writerCheckpoint,
					chaserCheckpoint, nodePriority);
				var prepareOk = CreatePrepareOk(index, epochId, lastCommitPosition, writerCheckpoint, chaserCheckpoint,
					nodePriority);
				prepareOks.Add(prepareOk.ServerId, prepareOk);
			}

			var lastElectedMaster = tc.LastElectedMaster.HasValue
				? (Guid?)IdForNode(tc.LastElectedMaster.Value)
				: null;
			var resigningMaster = tc.ResigningMaster.HasValue
				? (Guid?)IdForNode(tc.ResigningMaster.Value)
				: null;
			var mc = SUT.GetBestMasterCandidate(prepareOks, members, lastElectedMaster, resigningMaster);

			Assert.AreEqual(IdForNode(tc.ExpectedMasterCandidateNode), mc.InstanceId);

			var ownInfo = CreateMasterCandidate(1, epochId, lastCommitPosition, writerCheckpoint, chaserCheckpoint,
				nodePriority);

			var localNode = FromMember(0, members);

			var isLegit = SUT.IsLegitimateMaster(1, EndpointForNode(tc.ProposingNode),
				IdForNode(tc.ProposingNode), mc, members, null, localNode,
				ownInfo, resigningMaster);

			Assert.True(isLegit);
		}


		static ElectionMessage.PrepareOk CreatePrepareOk(int i, Guid epochId,
			Func<int, long> lastCommitPosition,
			Func<int, long> writerCheckpoint,
			Func<int, long> chaserCheckpoint,
			Func<int, int> nodePriority) {
			var id = IdForNode(i);
			var ep = EndpointForNode(i);
			return new ElectionMessage.PrepareOk(1, id, ep, 1, 1, epochId, lastCommitPosition(i), writerCheckpoint(i),
				chaserCheckpoint(i), nodePriority(i));
		}

		static SUT.MasterCandidate CreateMasterCandidate(int i, Guid epochId,
			Func<int, long> lastCommitPosition,
			Func<int, long> writerCheckpoint,
			Func<int, long> chaserCheckpoint,
			Func<int, int> nodePriority) {
			var id = IdForNode(i);
			var ep = EndpointForNode(i);
			return new SUT.MasterCandidate(id, ep, 1, 1, epochId, lastCommitPosition(i), writerCheckpoint(i),
				chaserCheckpoint(i), nodePriority(i));
		}

		static VNodeInfo FromMember(int index, MemberInfo[] members) {
			return new VNodeInfo(members[index].InstanceId, 1, members[index].InternalTcpEndPoint,
				members[index].InternalSecureTcpEndPoint, members[index].ExternalTcpEndPoint,
				members[index].ExternalSecureTcpEndPoint, members[index].InternalHttpEndPoint,
				members[index].ExternalHttpEndPoint, false);
		}

		static MemberInfo CreateMemberInfo(int i, Guid epochId, Func<int, long> lastCommitPosition,
			Func<int, long> writerCheckpoint,
			Func<int, long> chaserCheckpoint,
			Func<int, int> nodePriority) {
			var id = IdForNode(i);
			var ep = EndpointForNode(i);
			return MemberInfo.ForVNode(id, DateTime.Now, VNodeState.Slave, true, ep, ep, ep, ep, ep, ep,
				lastCommitPosition(i), writerCheckpoint(i), chaserCheckpoint(i), 1, 1, epochId, nodePriority(i), false);
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
			public int ExpectedMasterCandidateNode { get; set; }
			public int? ResigningMaster { get; set; }
			public int ProposingNode { get; set; }
			public int? LastElectedMaster { get; set; }
			public long[] CommitPositions { get; set; } = {1L, 1, 1};
			public long[] WriterCheckpoints { get; set; } = {1L, 1, 1};
			public long[] ChaserCheckpoints { get; set; } = {1L, 1, 1};
			public int[] NodePriorities { get; set; } = {1, 1, 1};

			private static string GenerateName(int expectedMasterCandidateNode, long[] commitPositions,
				long[] writerCheckpoints,
				long[] chaserCheckpoints, int[] nodePriorities) {
				var nameBuilder = new StringBuilder();
				if (commitPositions != null) {
					if (nameBuilder.Length == 0) nameBuilder.Append("Nodes with ");
					else nameBuilder.Append(" and");
					nameBuilder.AppendFormat("commit positions ( {0} )", string.Join(",",
						commitPositions.Where(x => x != 1).Select((x, i) => $"{i} : cp {x}")));
				}

				if (writerCheckpoints != null) {
					if (nameBuilder.Length == 0) nameBuilder.Append("Nodes with ");
					else nameBuilder.Append(" and ");
					nameBuilder.AppendFormat("writer checkpoints ( {0} )", string.Join(",",
						writerCheckpoints.Where(x => x != 1).Select((x, i) => $"{i} : wcp {x}")));
				}

				if (chaserCheckpoints != null) {
					if (nameBuilder.Length == 0) nameBuilder.Append("Nodes with ");
					else nameBuilder.Append(" and ");
					nameBuilder.AppendFormat("chaser checkpoints ( {0} )", string.Join(",",
						chaserCheckpoints.Where(x => x != 1).Select((x, i) => $"{i} : ccp {x}")));
				}

				if (nodePriorities != null) {
					if (nameBuilder.Length == 0) nameBuilder.Append("Nodes with ");
					else nameBuilder.Append(" and ");
					nameBuilder.AppendFormat("node priorities ( {0} )", string.Join(",",
						nodePriorities.Where(x => x != 0).Select((x, i) => $"{i} : np {x}")));
				}

				if (nameBuilder.Length == 0)
					nameBuilder.AppendFormat("All nodes caught up with the same priority expect {0} to be master",
						expectedMasterCandidateNode);
				var name = nameBuilder.ToString();
				return name;
			}

			public override string ToString() {
				return GenerateName(ExpectedMasterCandidateNode, CommitPositions, WriterCheckpoints, ChaserCheckpoints,
					NodePriorities);
			}
		}
	}
}
