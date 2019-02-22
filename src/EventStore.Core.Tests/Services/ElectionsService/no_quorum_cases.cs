using System.Linq;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.ElectionsService {
	[TestFixture]
	public sealed class elections_service_should_stuck_with_single_node_response {
		private ElectionsServiceUnit _electionsUnit;

		[SetUp]
		public void SetUp() {
			var clusterSettingsFactory = new ClusterSettingsFactory();
			var clusterSettings = clusterSettingsFactory.GetClusterSettings(1, 3);

			_electionsUnit = new ElectionsServiceUnit(clusterSettings);

			ProcessElections();
		}

		private void ProcessElections() {
			var gossipUpdate = new GossipMessage.GossipUpdated(_electionsUnit.ClusterInfo, new ClusterInfo());
			_electionsUnit.Publish(gossipUpdate);

			_electionsUnit.Publish(new ElectionMessage.StartElections());

			_electionsUnit.RepublishFromPublisher();
		}

		[Test]
		public void elect_node_with_biggest_port_ip_for_equal_writerchecksums() {
			Assert.That(_electionsUnit.Publisher.Messages.ContainsSingle<ElectionMessage.ElectionsTimedOut>());
			Assert.That(_electionsUnit.Publisher.Messages.ContainsSingle<ElectionMessage.SendViewChangeProof>());
		}
	}

	[TestFixture]
	public sealed class elections_service_should_stuck_with_single_node_response_2_iterations {
		private ElectionsServiceUnit _electionsUnit;

		[SetUp]
		public void SetUp() {
			var clusterSettingsFactory = new ClusterSettingsFactory();
			var clusterSettings = clusterSettingsFactory.GetClusterSettings(1, 3);

			_electionsUnit = new ElectionsServiceUnit(clusterSettings);

			ProcessElections();
		}

		private void ProcessElections() {
			var gossipUpdate = new GossipMessage.GossipUpdated(_electionsUnit.ClusterInfo, new ClusterInfo());
			_electionsUnit.Publish(gossipUpdate);

			_electionsUnit.Publish(new ElectionMessage.StartElections());

			_electionsUnit.RepublishFromPublisher();

			_electionsUnit.RepublishFromPublisher();
			Assert.That(
				_electionsUnit.Publisher.Messages.All(x => x is HttpMessage.SendOverHttp || x is TimerMessage.Schedule),
				Is.True,
				"Only OverHttp or Schedule messages are expected.");

			_electionsUnit.RepublishFromPublisher();
		}

		[Test]
		public void elect_node_with_biggest_port_ip_for_equal_writerchecksums() {
			Assert.That(_electionsUnit.Publisher.Messages.ContainsSingle<ElectionMessage.ElectionsTimedOut>());
			Assert.That(_electionsUnit.Publisher.Messages.ContainsSingle<ElectionMessage.SendViewChangeProof>());
		}
	}

	[TestFixture]
	public sealed class elections_service_should_stuck_with_single_alive_node {
		private ElectionsServiceUnit _electionsUnit;

		[SetUp]
		public void SetUp() {
			var clusterSettingsFactory = new ClusterSettingsFactory();
			var clusterSettings = clusterSettingsFactory.GetClusterSettings(1, 3);

			_electionsUnit = new ElectionsServiceUnit(clusterSettings);
			_electionsUnit.UpdateClusterMemberInfo(0, isAlive: false);
			_electionsUnit.UpdateClusterMemberInfo(2, isAlive: false);
			_electionsUnit.UpdateClusterMemberInfo(3, isAlive: false);

			ProcessElections();
		}

		private void ProcessElections() {
			var gossipUpdate = new GossipMessage.GossipUpdated(_electionsUnit.ClusterInfo, new ClusterInfo());
			_electionsUnit.Publish(gossipUpdate);

			_electionsUnit.Publish(new ElectionMessage.StartElections());

			_electionsUnit.RepublishFromPublisher();

			_electionsUnit.RepublishFromPublisher();
			Assert.That(
				_electionsUnit.Publisher.Messages.All(x => x is HttpMessage.SendOverHttp || x is TimerMessage.Schedule),
				Is.True,
				"Only OverHttp or Schedule messages are expected.");

			_electionsUnit.RepublishFromPublisher();

			_electionsUnit.RepublishFromPublisher();
			Assert.That(
				_electionsUnit.Publisher.Messages.All(x => x is HttpMessage.SendOverHttp || x is TimerMessage.Schedule),
				Is.True,
				"Only OverHttp or Schedule messages are expected.");

			_electionsUnit.RepublishFromPublisher();
		}

		[Test]
		public void elect_node_with_biggest_port_ip_for_equal_writerchecksums() {
			Assert.That(_electionsUnit.Publisher.Messages.ContainsSingle<ElectionMessage.ElectionsTimedOut>());
			Assert.That(_electionsUnit.Publisher.Messages.ContainsSingle<ElectionMessage.SendViewChangeProof>());
		}
	}

	[TestFixture]
	public sealed class elections_service_should_not_elect_nonpromotableclone {
		private ElectionsServiceUnit _electionsUnit;
		private const int SelfIndex = 0;
		private const int View = 2;

		[SetUp]
		public void SetUp() {
			var clusterSettingsFactory = new ClusterSettingsFactory();
			var clusterSettings = clusterSettingsFactory.GetClusterSettingsWithSelfAsNonPromotableClone(SelfIndex, 3);

			_electionsUnit = new ElectionsServiceUnit(clusterSettings);
			_electionsUnit.UpdateClusterMemberInfo(0, isAlive: true);
			_electionsUnit.UpdateClusterMemberInfo(1, isAlive: true);
			_electionsUnit.UpdateClusterMemberInfo(2, isAlive: false);

			StartElections();
			Prepare(SelfIndex, View);
		}

		private void StartElections() {
			var gossipUpdate = new GossipMessage.GossipUpdated(_electionsUnit.ClusterInfo, new ClusterInfo());
			_electionsUnit.Publish(gossipUpdate);

			_electionsUnit.Publish(new ElectionMessage.StartElections());

			_electionsUnit.RepublishFromPublisher();

			_electionsUnit.RepublishFromPublisher();
			Assert.That(_electionsUnit.Publisher.Messages.All(x => x is HttpMessage.SendOverHttp || x is TimerMessage.Schedule),
				Is.True,
				"Only OverHttp or Schedule messages are expected.");

			_electionsUnit.RepublishFromPublisher();

			_electionsUnit.RepublishFromPublisher();
			Assert.That(_electionsUnit.Publisher.Messages.All(x => x is HttpMessage.SendOverHttp || x is TimerMessage.Schedule),
				Is.True,
				"Only OverHttp or Schedule messages are expected.");

			_electionsUnit.RepublishFromPublisher();
		}

		private void Prepare(int selfIndex, int view) {
			_electionsUnit.Publish(new ElectionMessage.Prepare(_electionsUnit.ClusterInfo.Members[selfIndex].InstanceId,
				_electionsUnit.ClusterInfo.Members[selfIndex].ExternalTcpEndPoint, view));
		}

		[Test]
		public void prepare_node_for_election_fail_if_nonpromotableclone() {
			Assert.That(_electionsUnit.Publisher.Messages.ContainsSingle<ElectionMessage.PrepareKo>());
		}
	}
}
