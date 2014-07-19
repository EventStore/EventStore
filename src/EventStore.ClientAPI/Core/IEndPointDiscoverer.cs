using System.Net;
using System.Threading.Tasks;

namespace EventStore.ClientAPI.Core
{
    internal interface IEndPointDiscoverer
    {
        Task<NodeEndPoints> DiscoverAsync(IPEndPoint failedTcpEndPoint);
    }
}