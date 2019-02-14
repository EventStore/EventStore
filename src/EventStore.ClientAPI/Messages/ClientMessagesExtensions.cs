using System;
using System.Net;
using System.Runtime.ExceptionServices;
using EventStore.ClientAPI.Internal;

namespace EventStore.ClientAPI.Messages {
	internal static partial class ClientMessage {
		public partial class NotHandled {
			public partial class MasterInfo {
				public IPEndPoint ExternalTcpEndPoint {
					get { return new IPEndPoint(IPAddress.Parse(ExternalTcpAddress), ExternalTcpPort); }
				}

				public IPEndPoint ExternalSecureTcpEndPoint {
					get {
						return ExternalSecureTcpAddress == null || ExternalSecureTcpPort == null
							? null
							: new IPEndPoint(IPAddress.Parse(ExternalSecureTcpAddress), ExternalSecureTcpPort.Value);
					}
				}

				public IPEndPoint ExternalHttpEndPoint {
					get { return new IPEndPoint(IPAddress.Parse(ExternalHttpAddress), ExternalHttpPort); }
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
