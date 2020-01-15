using System;
using System.Runtime.Serialization;

namespace EventStore.Client {
	public class WrongExpectedVersionException : Exception {
		public string StreamName { get; }

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
		public WrongExpectedVersionException(string streamName, long? expectedVersion, long? actualVersion,
			Exception exception = default) :
			base(
				$"Append failed due to WrongExpectedVersion. Stream: {streamName}, Expected version: {expectedVersion}, Actual version: {actualVersion}",
				exception) {
			StreamName = streamName;
			ExpectedVersion = expectedVersion;
			ActualVersion = actualVersion;
		}
	}
}
