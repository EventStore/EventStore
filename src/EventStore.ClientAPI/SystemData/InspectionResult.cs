using System;
using System.Net;

namespace EventStore.ClientAPI.SystemData {
	internal class InspectionResult {
		public readonly InspectionDecision Decision;
		public readonly string Description;
		public readonly EndPoint TcpEndPoint;
		public readonly EndPoint SecureTcpEndPoint;

		public InspectionResult(InspectionDecision decision, string description, EndPoint tcpEndPoint = null,
			EndPoint secureTcpEndPoint = null) {
			if (decision == InspectionDecision.Reconnect) {
				if (tcpEndPoint == null && secureTcpEndPoint == null)
					throw new ArgumentNullException("Both TCP endpoints are null");
			} else {
				if (tcpEndPoint != null || secureTcpEndPoint != null)
					throw new ArgumentException(string.Format("tcpEndPoint or secureTcpEndPoint is not null for decision {0}.", decision));
			}

			Decision = decision;
			Description = description;
			TcpEndPoint = tcpEndPoint;
			SecureTcpEndPoint = secureTcpEndPoint;
		}
	}
}
