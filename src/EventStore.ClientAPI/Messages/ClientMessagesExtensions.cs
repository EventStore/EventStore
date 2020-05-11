using System;
using System.Net;
using System.Runtime.ExceptionServices;

namespace EventStore.ClientAPI.Messages {
	internal static partial class ClientMessage {
		public partial class NotHandled {
			public partial class LeaderInfo {
				public EndPoint ExternalTcpEndPoint {
					get { return ExternalTcpAddress == null ? null :
						new DnsEndPoint(ExternalTcpAddress, ExternalTcpPort); }
				}

				public EndPoint ExternalSecureTcpEndPoint {
					get {
						return ExternalSecureTcpAddress == null || ExternalSecureTcpPort == null
							? null
							: new DnsEndPoint(ExternalSecureTcpAddress, ExternalSecureTcpPort.Value);
					}
				}

				public EndPoint ExternalHttpEndPoint {
					get { return new DnsEndPoint(HttpAddress, HttpPort); }
				}
			}
		}

		public static ExceptionDispatchInfo InitializeSerializers() {
			try {
				foreach (var type in typeof(ClientMessage).GetNestedTypes()) {
					for (int i = 0; i < 12; i++) {
						try {
							ProtoBuf.Serializer.NonGeneric.PrepareSerializer(type);
							break;
						} catch (TimeoutException) when (i < 11
						) //if we are on the 11th try, we have taken 1 min trying to get the lock... may as well throw and die
						{
						}
					}
				}

				return null;
			} catch (Exception ex) {
				return ExceptionDispatchInfo.Capture(ex);
			}
		}
	}
}
