namespace EventStore.ClientAPI {
	/// <summary>
	/// Various constant values that may be useful when working with the ClientAPI.
	/// </summary>
	public static class ClientApiConstants {
		/// <summary>
		/// The maximum number of events that can be read in a single operation.
		/// </summary>
		public static readonly int MaxReadSize = Consts.MaxReadSize;
	}
}
