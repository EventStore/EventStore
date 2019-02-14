using System;
using System.Runtime.Serialization;

namespace EventStore.Transport.Tcp.Framing {
	public class PackageFramingException : Exception {
		public PackageFramingException() {
		}

		public PackageFramingException(string message) : base(message) {
		}

		public PackageFramingException(string message, Exception innerException) : base(message, innerException) {
		}

		protected PackageFramingException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}
	}
}
