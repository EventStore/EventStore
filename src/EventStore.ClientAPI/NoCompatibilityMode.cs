namespace EventStore.ClientAPI {
	/// <summary>
	/// Disables all compatibility mode.
	/// </summary>
	public class NoCompatibilityMode: ICompatibilityMode {
		/// <summary>
		/// Is EventStoreDB Version 5 compatibility mode enabled.
		/// </summary>
		public bool IsVersion5CompatibilityModeEnabled() {
			return false;
		}
	}
}
