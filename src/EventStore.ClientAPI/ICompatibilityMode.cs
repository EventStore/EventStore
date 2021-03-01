namespace EventStore.ClientAPI {
	/// <summary>
	/// Compatibility mode abstraction.
	/// </summary>
	public interface ICompatibilityMode {
		/// <summary>
		/// Is EventStoreDB Version 5 compatibility mode enabled.
		/// </summary>
		bool IsVersion5CompatibilityModeEnabled();

		/// <summary>
		/// Is EventStoreDB Auto compatibility mode enabled.
		/// </summary>
		bool IsAutoCompatibilityModeEnabled();
	}
}
