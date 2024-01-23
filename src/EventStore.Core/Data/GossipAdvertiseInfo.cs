using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.Data {
	public class GossipAdvertiseInfo {
		public DnsEndPoint InternalTcp { get; }
		public DnsEndPoint InternalSecureTcp { get; }
		public DnsEndPoint HttpEndPoint { get; }
		public string AdvertiseInternalHostAs { get; }
		public string AdvertiseExternalHostAs { get; }
		public int AdvertiseHttpPortAs { get; }
		public string AdvertiseHostToClientAs { get; }
		public int AdvertiseHttpPortToClientAs { get; }
		public int AdvertiseTcpPortToClientAs { get; }

		public GossipAdvertiseInfo(DnsEndPoint internalTcp, DnsEndPoint internalSecureTcp,
			DnsEndPoint httpEndPointEndPoint,
			string advertiseInternalHostAs, string advertiseExternalHostAs, int advertiseHttpPortAs,
			string advertiseHostToClientAs, int advertiseHttpPortToClientAs, int advertiseTcpPortToClientAs) {
			Ensure.Equal(false, internalTcp == null && internalSecureTcp == null, "Both internal TCP endpoints are null");

			InternalTcp = internalTcp;
			InternalSecureTcp = internalSecureTcp;
			HttpEndPoint = httpEndPointEndPoint;
			AdvertiseInternalHostAs = advertiseInternalHostAs;
			AdvertiseExternalHostAs = advertiseExternalHostAs;
			AdvertiseHttpPortAs = advertiseHttpPortAs;
			AdvertiseHostToClientAs = advertiseHostToClientAs;
			AdvertiseHttpPortToClientAs = advertiseHttpPortToClientAs;
			AdvertiseTcpPortToClientAs = advertiseTcpPortToClientAs;
		}

		public override string ToString() {
			return string.Format(
				$"IntTcp: {InternalTcp}, IntSecureTcp: {InternalSecureTcp}\n" +
				$"Http: {HttpEndPoint}, HttpAdvertiseAs: {AdvertiseExternalHostAs}:{AdvertiseHttpPortAs},\n" +
				$"HttpAdvertiseToClientAs: {AdvertiseHostToClientAs}:{AdvertiseHttpPortToClientAs},\n" +
				$"TcpAdvertiseToClientAs: {AdvertiseHostToClientAs}:{AdvertiseTcpPortToClientAs}");
		}
	}
}
