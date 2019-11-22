namespace EventStore.Grpc {
	public static class SystemStreams {
		public const string StreamsStream = "$streams";
		public const string SettingsStream = "$settings";
		public const string StatsStreamPrefix = "$stats";

		public static bool IsSystemStream(string streamId) => streamId.Length != 0 && streamId[0] == '$';
		public static string MetastreamOf(string streamId) => "$$" + streamId;
		public static bool IsMetastream(string streamId) => streamId.StartsWith("$$");
		public static string OriginalStreamOf(string metastreamId) => metastreamId.Substring(2);
	}

	///<summary>
	///Constants for information in stream metadata
	///</summary>
	internal static class SystemMetadata {
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

}
