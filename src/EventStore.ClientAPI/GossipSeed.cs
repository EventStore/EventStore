using System.Net;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents a source of cluster gossip.
    /// </summary>
    public class GossipSeed
    {
        /// <summary>
        /// The <see cref="IPEndPoint"/> for the External HTTP endpoint of the gossip seed.
        /// </summary>
        public readonly IPEndPoint EndPoint;
        /// <summary>
        /// The host header to be sent when requesting gossip.
        /// </summary>
        public readonly string HostHeader;

        /// <summary>
        /// Creates a new <see cref="GossipSeed" />.
        /// </summary>
        /// <param name="endPoint">The <see cref="IPEndPoint"/> for the External HTTP endpoint of the gossip seed.</param>
        /// <param name="hostHeader">The host header to be sent when requesting gossip. Defaults to String.Empty</param>
        public GossipSeed(IPEndPoint endPoint, string hostHeader = "")
        {
            EndPoint = endPoint;
            HostHeader = hostHeader;
        }
    }
}