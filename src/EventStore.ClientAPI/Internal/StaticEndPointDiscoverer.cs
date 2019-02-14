using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Internal {
	internal class StaticEndPointDiscoverer : IEndPointDiscoverer {
		private readonly Task<NodeEndPoints> _task;

		public StaticEndPointDiscoverer(IPEndPoint endPoint, bool isSsl) {
			Ensure.NotNull(endPoint, "endPoint");
			_task = Task.Factory.StartNew(() => new NodeEndPoints(isSsl ? null : endPoint, isSsl ? endPoint : null));
		}

		public Task<NodeEndPoints> DiscoverAsync(IPEndPoint failedTcpEndPoint) {
			return _task;
		}
	}
}
