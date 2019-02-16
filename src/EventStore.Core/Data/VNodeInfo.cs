using System;
using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.Data {
	public class VNodeInfo {
		public readonly Guid InstanceId;
		public readonly int DebugIndex;
		public readonly IPEndPoint InternalTcp;
		public readonly IPEndPoint InternalSecureTcp;
		public readonly IPEndPoint ExternalTcp;
		public readonly IPEndPoint ExternalSecureTcp;
		public readonly IPEndPoint InternalHttp;
		public readonly IPEndPoint ExternalHttp;

		public VNodeInfo(Guid instanceId, int debugIndex,
			IPEndPoint internalTcp, IPEndPoint internalSecureTcp,
			IPEndPoint externalTcp, IPEndPoint externalSecureTcp,
			IPEndPoint internalHttp, IPEndPoint externalHttp) {
			Ensure.NotEmptyGuid(instanceId, "instanceId");
			Ensure.NotNull(internalTcp, "internalTcp");
			Ensure.NotNull(externalTcp, "externalTcp");
			Ensure.NotNull(internalHttp, "internalHttp");
			Ensure.NotNull(externalHttp, "externalHttp");

			DebugIndex = debugIndex;
			InstanceId = instanceId;
			InternalTcp = internalTcp;
			InternalSecureTcp = internalSecureTcp;
			ExternalTcp = externalTcp;
			ExternalSecureTcp = externalSecureTcp;
			InternalHttp = internalHttp;
			ExternalHttp = externalHttp;
		}

		public bool Is(IPEndPoint endPoint) {
			return endPoint != null
			       && (InternalHttp.Equals(endPoint)
			           || ExternalHttp.Equals(endPoint)
			           || InternalTcp.Equals(endPoint)
			           || (InternalSecureTcp != null && InternalSecureTcp.Equals(endPoint))
			           || ExternalTcp.Equals(endPoint)
			           || (ExternalSecureTcp != null && ExternalSecureTcp.Equals(endPoint)));
		}

		public override string ToString() {
			return string.Format("InstanceId: {0:B}, InternalTcp: {1}, InternalSecureTcp: {2}, " +
			                     "ExternalTcp: {3}, ExternalSecureTcp: {4}, InternalHttp: {5}, ExternalHttp: {6}",
				InstanceId,
				InternalTcp,
				InternalSecureTcp,
				ExternalTcp,
				ExternalSecureTcp,
				InternalHttp,
				ExternalHttp);
		}
	}
}
