using System;

namespace EventStore.Core.Exceptions {
	public class NoCertificatePrivateKeyException : Exception {
		public NoCertificatePrivateKeyException() : base() {
		}
	}
}
