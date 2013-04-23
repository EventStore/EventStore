using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Core
{
    internal class StaticEndPointDiscoverer: IEndPointDiscoverer
    {
        private readonly Task<IPEndPoint> _task;

        public StaticEndPointDiscoverer(IPEndPoint endPoint)
        {
            Ensure.NotNull(endPoint, "endPoint");
            _task = Task.Factory.StartNew(() => endPoint);
        }

        public Task<IPEndPoint> DiscoverAsync()
        {
            return _task;
        }
    }
}