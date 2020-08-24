using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.Data {
	public class GossipAdvertiseInfo {
		public DnsEndPoint InternalTcp { get; set; }
		public DnsEndPoint InternalSecureTcp { get; set; }
		public DnsEndPoint ExternalTcp { get; set; }
		public DnsEndPoint ExternalSecureTcp { get; set; }
		public DnsEndPoint HttpEndPoint { get; set; }
		public string AdvertiseInternalHostAs { get; set; }
		public string AdvertiseExternalHostAs { get; set; }
		public int AdvertiseHttpPortAs { get; set; }
		public string AdvertiseHostToClientAs { get; set; }
		public int AdvertiseHttpPortToClientAs { get; set; }
		public int AdvertiseTcpPortToClientAs { get; set; }

		public GossipAdvertiseInfo(DnsEndPoint internalTcp, DnsEndPoint internalSecureTcp,
			DnsEndPoint externalTcp, DnsEndPoint externalSecureTcp,
			DnsEndPoint httpEndPointEndPoint,
			string advertiseInternalHostAs, string advertiseExternalHostAs, int advertiseHttpPortAs,
			string advertiseHostToClientAs, int advertiseHttpPortToClientAs, int advertiseTcpPortToClientAs) {
			Ensure.Equal(false, internalTcp == null && internalSecureTcp == null, "Both internal TCP endpoints are null");

			InternalTcp = internalTcp;
			InternalSecureTcp = internalSecureTcp;
			ExternalTcp = externalTcp;
			ExternalSecureTcp = externalSecureTcp;
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
				$"ExtTcp: {ExternalTcp}, ExtSecureTcp: {ExternalSecureTcp}\n" +
				$"Http: {HttpEndPoint}, HttpAdvertiseAs: {AdvertiseExternalHostAs}:{AdvertiseHttpPortAs},\n" +
				$"HttpAdvertiseToClientAs: {AdvertiseHostToClientAs}:{AdvertiseHttpPortToClientAs},\n" +
				$"TcpAdvertiseToClientAs: {AdvertiseHostToClientAs}:{AdvertiseTcpPortToClientAs}");
		}
	}
}
