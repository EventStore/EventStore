using System;
using System.Runtime.Serialization;

namespace EventStore.Grpc {
	public class WrongExpectedVersionException : Exception {
		/// <summary>
		/// If available, the expected version specified for the operation that failed.
		/// </summary>
		public long? ExpectedVersion { get; }

		/// <summary>
		/// If available, the current version of the stream that the operation was attempted on.
		/// </summary>
		public long? ActualVersion { get; }

		/// <summary>
		/// Constructs a new instance of <see cref="WrongExpectedVersionException" /> with the expected and actual versions if available.
		/// </summary>
		public WrongExpectedVersionException(string message, long? expectedVersion, long? actualVersion,
			Exception exception = default) :
			base(message, exception) {
			ExpectedVersion = expectedVersion;
			ActualVersion = actualVersion;
		}
	}
}
