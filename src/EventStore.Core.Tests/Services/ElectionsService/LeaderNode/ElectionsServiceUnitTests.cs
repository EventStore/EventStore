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
using EventStore.Core.TransactionLogV2.Checkpoint;
using NUnit.Framework;
using SUT = EventStore.Core.Services.ElectionsService;

namespace EventStore.Core.Tests.Services.ElectionsService {
	public class ElectionsServiceUnitTests {
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
				var memberInfo = MemberInfo.ForVNode(instanceId, DateTime.UtcNow, VNodeState.Unknown, true,
					endPoint, null, endPoint, null, endPoint, null, 0, 0, -1, 0, 0, -1, -1, Guid.Empty, 0, false);
				members.Add(memberInfo);
				_fakeTimeProvider = new FakeTimeProvider();
				_scheduler = new FakeScheduler(new FakeTimer(), _fakeTimeProvider);
				var timerService = new TimerService(_scheduler);

				var writerCheckpoint = new InMemoryCheckpoint();
				var readerCheckpoint = new InMemoryCheckpoint();
				var epochManager = new FakeEpochManager();
				Func<long> lastCommitPosition = () => -1;
				var electionsService = new Core.Services.ElectionsService(outputBus,
					memberInfo,
					3,
					writerCheckpoint,
					readerCheckpoint,
					epochManager,
					() => -1, 0, new FakeTimeProvider());
				electionsService.SubscribeMessages(inputBus);

				var nodeId = i;
				outputBus.Subscribe(new AdHocHandler<Message>(
					m => {
						switch (m) {
							case TimerMessage.Schedule sm:
								TestContext.WriteLine(
									$"Node {nodeId} : Delay {sm.TriggerAfter} : {sm.ReplyMessage.GetType()}");
								timerService.Handle(sm);
								break;
							default:
								TestContext.WriteLine($"Node {nodeId} : EP {m.GetType()}");
								inputBus.Publish(m);
								break;
						}
					}
				));
				_nodes.Add(endPoint, inputBus);

				var gossip = new NodeGossipService(outputBus, seedSource, memberInfo, writerCheckpoint, readerCheckpoint,
					epochManager, lastCommitPosition, 0, TimeSpan.FromMilliseconds(500), TimeSpan.FromDays(1),
					TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1800), _fakeTimeProvider);
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

		class ReallyNotSafeFakeGossipSeedSource : IGossipSeedSource {
			private readonly List<IPEndPoint> _ipEndPoints;

			public ReallyNotSafeFakeGossipSeedSource(List<IPEndPoint> ipEndPoints) {
				_ipEndPoints = ipEndPoints;
			}

			public IAsyncResult BeginGetHostEndpoints(AsyncCallback requestCallback, object state) {
				requestCallback(null);
				return null;
			}

			public EndPoint[] EndGetHostEndpoints(IAsyncResult asyncResult) {
				return _ipEndPoints.ToArray();
			}
		}
	}

	public class ChoosingLeaderTests {
		static IEnumerable<TestCase> CreateCases() {
			//Basic cases
			yield return new TestCase {
				ExpectedLeaderCandidateNode = 2,
				ProposingNode = 0,
			};

			yield return new TestCase {
				ExpectedLeaderCandidateNode = 0,
				NodePriorities = new[] {3, 2, 1}
			};

			yield return new TestCase {
				ExpectedLeaderCandidateNode = 1,
				NodePriorities = new[] {0, 0, int.MinValue},
				LastElectedLeader = 2,
				ResigningLeader = 2
			};

			yield return new TestCase {
				ExpectedLeaderCandidateNode = 0,
				NodePriorities = new[] {int.MinValue, int.MinValue, int.MinValue},
				LastElectedLeader = 0,
			};

			yield return new TestCase {
				ExpectedLeaderCandidateNode = 0,
				CommitPositions = new long[] {1, 0, 1},
				NodePriorities = new[] {0, 0, int.MinValue},
				LastElectedLeader = 2,
				ResigningLeader = 2
			};
			
			yield return new TestCase {
				ExpectedLeaderCandidateNode = 1,
				WriterCheckpoints = new long[] {0, 1, 1},
				NodePriorities = new[] {0, 0, int.MinValue},
				LastElectedLeader = 2,
				ResigningLeader = 2
			};
			
			yield return new TestCase {
				ExpectedLeaderCandidateNode = 0,
				ChaserCheckpoints = new long[] {1, 0, 1},
				NodePriorities = new[] {0, 0, int.MinValue},
				LastElectedLeader = 2,
				ResigningLeader = 2
			};

			yield return new TestCase {
				ExpectedLeaderCandidateNode = 0,
				CommitPositions = new long[] {1, 0, 0},
				NodePriorities = new[] {int.MinValue, 0, 0},
				LastElectedLeader = 0
			};
			
			yield return new TestCase {
				ExpectedLeaderCandidateNode = 1,
				WriterCheckpoints = new long[] {0, 1, 0},
				NodePriorities = new[] {0, int.MinValue, 0},
				LastElectedLeader = 1
			};
			
			yield return new TestCase {
				ExpectedLeaderCandidateNode = 2,
				ChaserCheckpoints = new long[] {0, 0, 1},
				NodePriorities = new[] {0, 0, int.MinValue},
				LastElectedLeader = 2
			};

			yield return new TestCase {
				ExpectedLeaderCandidateNode = 0,
				NodePriorities = new[] {0, int.MinValue, int.MinValue},
				LastElectedLeader = 1,
				ResigningLeader = 1
			};

			yield return new TestCase {
				ExpectedLeaderCandidateNode = 1,
				NodePriorities = new[] {int.MinValue, 0, int.MinValue},
				LastElectedLeader = 0,
				ResigningLeader = 0
			};

			yield return new TestCase {
				ExpectedLeaderCandidateNode = 2,
				NodePriorities = new[] {int.MinValue, int.MinValue, 0},
				LastElectedLeader = 1,
				ResigningLeader = 1
			};

			yield return new TestCase {
				ExpectedLeaderCandidateNode = 2,
				CommitPositions = new long[] {1, 0, 1},
				NodePriorities = new[] {int.MinValue, 0, int.MinValue},
				LastElectedLeader = 0,
				ResigningLeader = 0
			};
			
			yield return new TestCase {
				ExpectedLeaderCandidateNode = 2,
				WriterCheckpoints = new long[] {1, 0, 1},
				NodePriorities = new[] {int.MinValue, 0, int.MinValue},
				LastElectedLeader = 0,
				ResigningLeader = 0
			};
			
			yield return new TestCase {
				ExpectedLeaderCandidateNode = 2,
				ChaserCheckpoints = new long[] {1, 0, 1},
				NodePriorities = new[] {int.MinValue, 0, int.MinValue},
				LastElectedLeader = 0,
				ResigningLeader = 0
			};
		}

		[Test, TestCaseSource(nameof(TestCases))]
		public void should_select_valid_best_leader_candidate(TestCase tc) {
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
			}

			var clusterInfo = new ClusterInfo(members);
			var previousLeaderId = tc.LastElectedLeader.HasValue ? IdForNode(tc.LastElectedLeader.Value) : Guid.Empty;

			for (int index = 0; index < 3; index++) {
				var prepareOk = CreatePrepareOk(index, epochId, lastCommitPosition, writerCheckpoint, chaserCheckpoint,
					nodePriority, previousLeaderId, clusterInfo);
				prepareOks.Add(prepareOk.ServerId, prepareOk);
			}

			var lastElectedLeader = tc.LastElectedLeader.HasValue
				? (Guid?)IdForNode(tc.LastElectedLeader.Value)
				: null;
			var resigningLeadership = tc.ResigningLeader.HasValue
				? (Guid?)IdForNode(tc.ResigningLeader.Value)
				: null;
			var mc = SUT.GetBestLeaderCandidate(prepareOks, members, resigningLeadership, 0);

			Assert.AreEqual(IdForNode(tc.ExpectedLeaderCandidateNode), mc.InstanceId);

			var ownInfo = CreateLeaderCandidate(1, epochId, lastCommitPosition, writerCheckpoint, chaserCheckpoint,
				nodePriority, previousLeaderId);

			var isLegit = SUT.IsLegitimateLeader(1, EndpointForNode(tc.ProposingNode),
				IdForNode(tc.ProposingNode), mc, members, null, members[0].InstanceId,
				ownInfo, resigningLeadership);

			Assert.True(isLegit);
		}


		static ElectionMessage.PrepareOk CreatePrepareOk(int i, Guid epochId,
			Func<int, long> lastCommitPosition,
			Func<int, long> writerCheckpoint,
			Func<int, long> chaserCheckpoint,
			Func<int, int> nodePriority,
			Guid previousLeaderId,
			ClusterInfo clusterInfo) {
			var id = IdForNode(i);
			var ep = EndpointForNode(i);
			return new ElectionMessage.PrepareOk(1, id, ep, 1, 1, epochId, previousLeaderId, lastCommitPosition(i), writerCheckpoint(i),
				chaserCheckpoint(i), nodePriority(i), clusterInfo);
		}

		static SUT.LeaderCandidate CreateLeaderCandidate(int i, Guid epochId,
			Func<int, long> lastCommitPosition,
			Func<int, long> writerCheckpoint,
			Func<int, long> chaserCheckpoint,
			Func<int, int> nodePriority,
			Guid previousLeaderId) {
			var id = IdForNode(i);
			var ep = EndpointForNode(i);
			return new SUT.LeaderCandidate(id, ep, 1, 1, epochId, previousLeaderId, lastCommitPosition(i), writerCheckpoint(i),
				chaserCheckpoint(i), nodePriority(i));
		}

		static MemberInfo CreateMemberInfo(int i, Guid epochId, Func<int, long> lastCommitPosition,
			Func<int, long> writerCheckpoint,
			Func<int, long> chaserCheckpoint,
			Func<int, int> nodePriority) {
			var id = IdForNode(i);
			var ep = EndpointForNode(i);
			return MemberInfo.ForVNode(id, DateTime.Now, VNodeState.Follower, true, ep, ep, ep, ep, ep, null, 0, 0,
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
			public int ExpectedLeaderCandidateNode { get; set; }
			public int? ResigningLeader { get; set; }
			public int ProposingNode { get; set; }
			public int? LastElectedLeader { get; set; }
			public long[] CommitPositions { get; set; } = {1L, 1, 1};
			public long[] WriterCheckpoints { get; set; } = {1L, 1, 1};
			public long[] ChaserCheckpoints { get; set; } = {1L, 1, 1};
			public int[] NodePriorities { get; set; } = {1, 1, 1};

			private static string GenerateName(int expectedLeaderCandidateNode, long[] commitPositions,
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
					nameBuilder.AppendFormat("All nodes caught up with the same priority expect {0} to be leader",
						expectedLeaderCandidateNode);
				var name = nameBuilder.ToString();
				return name;
			}

			public override string ToString() {
				return GenerateName(ExpectedLeaderCandidateNode, CommitPositions, WriterCheckpoints, ChaserCheckpoints,
					NodePriorities);
			}
		}
	}
}
