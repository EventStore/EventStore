using System;
using System.Net;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.SystemData {
	internal class InspectionResult {
		public readonly InspectionDecision Decision;
		public readonly string Description;
		public readonly IPEndPoint TcpEndPoint;
		public readonly IPEndPoint SecureTcpEndPoint;

		public InspectionResult(InspectionDecision decision, string description, IPEndPoint tcpEndPoint = null,
			IPEndPoint secureTcpEndPoint = null) {
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
