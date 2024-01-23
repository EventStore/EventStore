using System;

namespace EventStore.Core.Services.Transport.Tcp {
	public enum ClientVersion : byte {
		V1 = 0,
		V2 = 1
	}

	public class ClientWriteTcpDispatcher : TcpDispatcher {
		private readonly TimeSpan _writeTimeout;

		protected ClientWriteTcpDispatcher(TimeSpan writeTimeout) {
			_writeTimeout = writeTimeout;
		}
	}
}
