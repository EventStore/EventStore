using EventStore.Core;
using EventStore.Projections.Core;

namespace EventStore.ClientAPI.Embedded {
	/// <summary>
	/// Allows a client to build a <see cref="ClusterVNode" /> for use with the Embedded client API by specifying
	/// high level options rather than using the constructor of <see cref="ClusterVNode"/> directly.
	/// </summary>
	public class EmbeddedVNodeBuilder : VNodeBuilder {
		private EmbeddedVNodeBuilder() {
		}

		/// <summary>
		/// Returns a builder set to construct options for a single node instance.
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set.</returns>
		public static EmbeddedVNodeBuilder AsSingleNode() {
			var ret = new EmbeddedVNodeBuilder().WithSingleNodeSettings();
			return (EmbeddedVNodeBuilder)ret;
		}

		/// <summary>
		/// Returns a builder set to construct options for a cluster node instance with a cluster size.
		/// </summary>
		/// <returns>A <see cref="VNodeBuilder"/> with the options set.</returns>
		public static EmbeddedVNodeBuilder AsClusterMember(int clusterSize) {
			var ret = new EmbeddedVNodeBuilder().WithClusterNodeSettings(clusterSize);
			return (EmbeddedVNodeBuilder)ret;
		}

		/// <summary>
		/// Sets up the projections subsystem.
		/// </summary>
		protected override void SetUpProjectionsIfNeeded() {
			_subsystems.Add(new ProjectionsSubsystem(_projectionsThreads, _projectionType,
				_startStandardProjections, _projectionsQueryExpiry, _faultOutOfOrderProjections));
		}
	}
}
