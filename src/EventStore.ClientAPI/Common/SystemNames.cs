namespace EventStore.ClientAPI.Common {
	static class SystemStreams {
		public const string StreamsStream = "$streams";
		public const string SettingsStream = "$settings";
		public const string StatsStreamPrefix = "$stats";

		public static string MetastreamOf(string streamId) {
			return "$$" + streamId;
		}

		public static bool IsMetastream(string streamId) {
			return streamId.StartsWith("$$");
		}

		public static string OriginalStreamOf(string metastreamId) {
			return metastreamId.Substring(2);
		}
	}

	///<summary>
	///Constants for information in stream metadata
	///</summary>
	public static class SystemMetadata {
		///<summary>
		///The definition of the MaxAge value assigned to stream metadata
		///Setting this allows all events older than the limit to be deleted
		///</summary>
		public const string MaxAge = "$maxAge";

		///<summary>
		///The definition of the MaxCount value assigned to stream metadata
		///setting this allows all events with a sequence less than current -maxcount to be deleted
		///</summary>
		public const string MaxCount = "$maxCount";

		///<summary>
		///The definition of the Truncate Before value assigned to stream metadata
		///setting this allows all events prior to the integer value to be deleted
		///</summary>
		public const string TruncateBefore = "$tb";

		///<summary>
		/// Sets the cache control in seconds for the head of the stream.
		///</summary>
		public const string CacheControl = "$cacheControl";


		///<summary>
		/// The acl definition in metadata
		///</summary>
		public const string Acl = "$acl";

		///<summary>
		/// to read from a stream
		///</summary>
		public const string AclRead = "$r";

		///<summary>
		/// to write to a stream
		///</summary>
		public const string AclWrite = "$w";

		///<summary>
		/// to delete a stream
		///</summary>
		public const string AclDelete = "$d";

		///<summary>
		/// to read metadata 
		///</summary>
		public const string AclMetaRead = "$mr";

		///<summary>
		/// to write metadata 
		///</summary>
		public const string AclMetaWrite = "$mw";


		///<summary>
		/// The user default acl stream 
		///</summary>
		public const string UserStreamAcl = "$userStreamAcl";

		///<summary>
		/// the system stream defaults acl stream 
		///</summary>
		public const string SystemStreamAcl = "$systemStreamAcl";
	}


	///<summary>
	///Constants for System event types
	///</summary>
	public static class SystemEventTypes {
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

	/// <summary>
	/// System supported consumer strategies for use with persistent subscriptions.
	/// </summary>
	public static class SystemConsumerStrategies {
		/// <summary>
		/// Distributes events to a single client until it is full. Then round robin to the next client.
		/// </summary>
		public const string DispatchToSingle = "DispatchToSingle";

		/// <summary>
		/// Distribute events to each client in a round robin fashion.
		/// </summary>
		public const string RoundRobin = "RoundRobin";

		/// <summary>
		/// Distribute events of the same streamId to the same client until it disconnects on a best efforts basis. 
		/// Designed to be used with indexes such as the category projection.
		/// </summary>
		public const string Pinned = "Pinned";
	}
}
