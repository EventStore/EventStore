using System;
using System.Runtime.Serialization;

namespace EventStore.Transport.Tcp.Framing {
	public class PackageFramingException : Exception {
		public PackageFramingException(string message) : base(message) {
		}
	}
}
