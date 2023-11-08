using System;
using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.Data {
	public class VNodeInfo {
		public readonly Guid InstanceId;
		public readonly int DebugIndex;
		public readonly IPEndPoint InternalTcp;
		public readonly IPEndPoint InternalSecureTcp;
		public readonly EndPoint HttpEndPoint;
		public readonly bool IsReadOnlyReplica;

		public VNodeInfo(Guid instanceId, int debugIndex,
			IPEndPoint internalTcp, IPEndPoint internalSecureTcp,
			EndPoint httpEndPoint,
			bool isReadOnlyReplica) {
			Ensure.NotEmptyGuid(instanceId, "instanceId");
			Ensure.Equal(false, internalTcp == null && internalSecureTcp == null, "Both internal TCP endpoints are null");
			Ensure.NotNull(httpEndPoint, nameof(httpEndPoint));

			DebugIndex = debugIndex;
			InstanceId = instanceId;
			InternalTcp = internalTcp;
			InternalSecureTcp = internalSecureTcp;
			HttpEndPoint = httpEndPoint;
			IsReadOnlyReplica = isReadOnlyReplica;
		}

		public bool Is(EndPoint endPoint) {
			return endPoint != null
			       && HttpEndPoint.Equals(endPoint)
			       || (InternalTcp != null && InternalTcp.Equals(endPoint))
			       || (InternalSecureTcp != null && InternalSecureTcp.Equals(endPoint));
		}

		public override string ToString() {
			return string.Format("InstanceId: {0:B}, InternalTcp: {1}, InternalSecureTcp: {2}, " +
			                     "HttpEndPoint: {3}, IsReadOnlyReplica: {4}",
				InstanceId,
				InternalTcp,
				InternalSecureTcp,
				HttpEndPoint,
				IsReadOnlyReplica);
		}
	}
}
