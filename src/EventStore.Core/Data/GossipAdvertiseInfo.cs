using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.Data {
	public class GossipAdvertiseInfo {
		public DnsEndPoint InternalTcp { get; set; }
		public DnsEndPoint InternalSecureTcp { get; set; }
		public DnsEndPoint ExternalTcp { get; set; }
		public DnsEndPoint ExternalSecureTcp { get; set; }
		public DnsEndPoint InternalHttp { get; set; }
		public DnsEndPoint ExternalHttp { get; set; }
		public string AdvertiseInternalHostAs { get; set; }
		public string AdvertiseExternalHostAs { get; set; }
		public int AdvertiseInternalHttpPortAs { get; set; }
		public int AdvertiseExternalHttpPortAs { get; set; }

		public GossipAdvertiseInfo(DnsEndPoint internalTcp, DnsEndPoint internalSecureTcp,
			DnsEndPoint externalTcp, DnsEndPoint externalSecureTcp,
			DnsEndPoint internalHttp, DnsEndPoint externalHttp,
			string advertiseInternalHostAs, string advertiseExternalHostAs,
			int advertiseInternalHttpPortAs, int advertiseExternalHttpPortAs) {
			Ensure.Equal(false, internalTcp == null && internalSecureTcp == null, "Both internal TCP endpoints are null");

			InternalTcp = internalTcp;
			InternalSecureTcp = internalSecureTcp;
			ExternalTcp = externalTcp;
			ExternalSecureTcp = externalSecureTcp;
			InternalHttp = internalHttp;
			ExternalHttp = externalHttp;
			AdvertiseInternalHostAs = advertiseInternalHostAs;
			AdvertiseExternalHostAs = advertiseExternalHostAs;
			AdvertiseInternalHttpPortAs = advertiseInternalHttpPortAs;
			AdvertiseExternalHttpPortAs = advertiseExternalHttpPortAs;
		}

		public override string ToString() {
			return string.Format(
				"IntTcp: {0}, IntSecureTcp: {1}\nExtTcp: {2}, ExtSecureTcp: {3}\nIntHttp: {4}, ExtHttp: {5}, IntAdvertiseAs: {6}:{7}, ExtAdvertiseAs: {8}:{9}",
				InternalTcp, InternalSecureTcp, ExternalTcp, ExternalSecureTcp, InternalHttp, ExternalHttp,
				AdvertiseInternalHostAs, AdvertiseInternalHttpPortAs, AdvertiseExternalHostAs, AdvertiseExternalHttpPortAs);
		}
	}
}
