using EventStore.Core.Cluster.Settings;

namespace EventStore.Core.Tests.Common.VNodeBuilderTests {
	public class TestVNodeBuilder : VNodeBuilder {
		protected TestVNodeBuilder() {
		}

		public static TestVNodeBuilder AsSingleNode() {
			var ret = new TestVNodeBuilder().WithSingleNodeSettings();
			return (TestVNodeBuilder)ret;
		}

		public static TestVNodeBuilder AsClusterMember(int clusterSize) {
			var ret = new TestVNodeBuilder().WithClusterNodeSettings(clusterSize);
			return (TestVNodeBuilder)ret;
		}

		protected override void SetUpProjectionsIfNeeded() {
		}

		public ClusterVNodeSettings GetSettings() {
			return _vNodeSettings;
		}
	}
}
