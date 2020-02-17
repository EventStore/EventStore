using System.Net;
using System.Threading.Tasks;

namespace EventStore.Client {
	public interface IEndpointDiscoverer {
		Task<IPEndPoint> DiscoverAsync();
	}
}
