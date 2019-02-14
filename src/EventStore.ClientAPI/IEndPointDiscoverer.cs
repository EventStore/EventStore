using System.Net;
using System.Threading.Tasks;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents a way of discovering cluster endpoints. This could be through gossip, consul, text files, etc
	/// </summary>
	public interface IEndPointDiscoverer {
		/// <summary>
		/// Called to discover a new <see cref="IPEndPoint"/>
		/// </summary>
		/// <param name="failedTcpEndPoint">The <see cref="IPEndPoint"/>The recently failed endpoint</param>
		Task<NodeEndPoints> DiscoverAsync(IPEndPoint failedTcpEndPoint);
	}
}
