namespace EventStore.TestClient {
	/// <summary>
	/// Standalone command executable within the test client
	/// </summary>
	public interface ICmdProcessor {
		/// <summary>
		/// Keyword associated with this command (to start it)
		/// </summary>
		string Keyword { get; }

		/// <summary>
		/// Short usage string with optional and required parameters
		/// </summary>
		string Usage { get; }

		bool Execute(CommandProcessorContext context, string[] args);
	}
}
