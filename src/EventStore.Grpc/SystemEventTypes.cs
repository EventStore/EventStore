namespace EventStore.Grpc {
	///<summary>
	///Constants for System event types
	///</summary>
	internal static class SystemEventTypes {
		///<summary>
		/// event type for stream deleted
		///</summary>
		public const string StreamDeleted = "$streamDeleted";

		///<summary>
		/// event type for statistics
		///</summary>
		public const string StatsCollection = "$statsCollected";

		///<summary>
		/// event type for linkTo 
		///</summary>
		public const string LinkTo = "$>";

		///<summary>
		/// event type for stream metadata 
		///</summary>
		public const string StreamMetadata = "$metadata";

		///<summary>
		/// event type for the system settings 
		///</summary>
		public const string Settings = "$settings";
	}
}
