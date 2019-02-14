namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown if an unexpected command is received.
	/// </summary>
	public class CommandNotExpectedException : EventStoreConnectionException {
		/// <summary>
		/// Constructs a new <see cref="CommandNotExpectedException" />.
		/// </summary>
		public CommandNotExpectedException(string expected, string actual)
			: base(string.Format("Expected : {0}. Actual : {1}.", expected, actual)) {
		}

		/// <summary>
		/// Constructs a new <see cref="CommandNotExpectedException" />.
		/// </summary>
		public CommandNotExpectedException(string unexpectedCommand)
			: base(string.Format("Unexpected command: {0}.", unexpectedCommand)) {
		}
	}
}
