using System.Net;
using System.Threading.Tasks;

namespace EventStore.ClientAPI.Internal
{
    internal interface IEndPointDiscoverer
    {
        Task<NodeEndPoints> DiscoverAsync(IPEndPoint failedTcpEndPoint);
    }
}