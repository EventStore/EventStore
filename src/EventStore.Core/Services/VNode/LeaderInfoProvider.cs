using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EndPoint = System.Net.EndPoint;


namespace EventStore.Core.Services.VNode {

	public class LeaderInfoProvider {
		private readonly GossipAdvertiseInfo _gossipInfo;
		private readonly Cluster.MemberInfo _leaderInfo;

		public LeaderInfoProvider(GossipAdvertiseInfo gossipInfo, Cluster.MemberInfo leaderInfo) {
			Ensure.NotNull(gossipInfo, "gossipInfo");
			
			_gossipInfo = gossipInfo;
			_leaderInfo = leaderInfo;
		}

		public EndPoint
			GetLeaderInfoEndPoint(){

			var endpoints = _leaderInfo != null 
				? (HttpEndPoint: _leaderInfo.HttpEndPoint,
					AdvertiseHost: _leaderInfo.AdvertiseHostToClientAs,
					AdvertiseHttpPort: _leaderInfo.AdvertiseHttpPortToClientAs,
					AdvertiseTcpPort: _leaderInfo.AdvertiseTcpPortToClientAs)
				: (HttpEndPoint: _gossipInfo.HttpEndPoint,
					AdvertiseHost: _gossipInfo.AdvertiseHostToClientAs,
					AdvertiseHttpPort: _gossipInfo.AdvertiseHttpPortToClientAs,
					AdvertiseTcpPort: _gossipInfo.AdvertiseTcpPortToClientAs);

			var advertisedHttpEndPoint = new DnsEndPoint(
				string.IsNullOrEmpty(endpoints.AdvertiseHost)
					? endpoints.HttpEndPoint.GetHost()
					: endpoints.AdvertiseHost,
				endpoints.AdvertiseHttpPort == 0 ? endpoints.HttpEndPoint.GetPort() : endpoints.AdvertiseHttpPort);

			return advertisedHttpEndPoint;
		}
	}
}
