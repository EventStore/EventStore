using System.Net;
using EventStore.Common;
using EventStore.Common.Utils;

namespace EventStore.Core.Data {
	public class GossipAdvertiseInfo {
		public EventStoreEndPoint InternalTcp { get; set; }
		public EventStoreEndPoint InternalSecureTcp { get; set; }
		public EventStoreEndPoint ExternalTcp { get; set; }
		public EventStoreEndPoint ExternalSecureTcp { get; set; }
		public EventStoreEndPoint InternalHttp { get; set; }
		public EventStoreEndPoint ExternalHttp { get; set; }
		public string AdvertiseInternalHostAs { get; set; }
		public string AdvertiseExternalHostAs { get; set; }
		public int AdvertiseInternalHttpPortAs { get; set; }
		public int AdvertiseExternalHttpPortAs { get; set; }

		public GossipAdvertiseInfo(EventStoreEndPoint internalTcp, EventStoreEndPoint internalSecureTcp,
			EventStoreEndPoint externalTcp, EventStoreEndPoint externalSecureTcp,
			EventStoreEndPoint internalHttp, EventStoreEndPoint externalHttp,
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
