using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown if the expected version specified on an operation
	/// does not match the version of the stream when the operation was attempted. 
	/// </summary>
	public class WrongExpectedVersionException : EventStoreConnectionException {
		/// <summary>
		/// If available, the expected version specified for the operation that failed.
		/// </summary>
		/// <remarks>Only available if the operation was <see cref="IEventStoreConnection.AppendToStreamAsync(string,long,EventStore.ClientAPI.EventData[])"/> or one of it's overloads.</remarks>
		public long? ExpectedVersion { get; }

		/// <summary>
		/// If available, the current version of the stream that the operation was attempted on.
		/// </summary>
		/// <remarks>Only available if the operation was <see cref="IEventStoreConnection.AppendToStreamAsync(string,long,EventStore.ClientAPI.EventData[])"/> or one of it's overloads.</remarks>
		public long? ActualVersion { get; }

		/// <summary>
		/// Constructs a new instance of <see cref="WrongExpectedVersionException" />.
		/// </summary>
		public WrongExpectedVersionException(string message) : base(message) {
		}

		/// <summary>
		/// Constructs a new instance of <see cref="WrongExpectedVersionException" /> with the expected and actual versions if available.
		/// </summary>
		public WrongExpectedVersionException(string message, long? expectedVersion, long? actualVersion) :
			base(message) {
			ExpectedVersion = expectedVersion;
			ActualVersion = actualVersion;
		}

		/// <summary>
		/// Constructs a new instance of <see cref="WrongExpectedVersionException" />.
		/// </summary>
		public WrongExpectedVersionException(string message, Exception innerException) : base(message, innerException) {
		}

		/// <summary>
		/// Constructs a new instance of <see cref="WrongExpectedVersionException" />.
		/// </summary>
		protected WrongExpectedVersionException(SerializationInfo info, StreamingContext context) :
			base(info, context) {
		}
	}
}
