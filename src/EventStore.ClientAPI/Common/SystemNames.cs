namespace EventStore.ClientAPI.Common
{
    internal static class SystemStreams
    {
        public const string StreamsStream = "$streams";
        public const string SettingsStream = "$settings";
        public const string StatsStreamPrefix = "$stats";
        public const string AllStream = "$all";
        public const string PersistentSubscriptionConfig = "$persistentSubscriptionConfig";

        public static string MetastreamOf(string streamId)
        {
            return "$$" + streamId;
        }

        public static bool IsMetastream(string streamId)
        {
            return streamId.StartsWith("$$");
        }

        public static string OriginalStreamOf(string metastreamId)
        {
            return metastreamId.Substring(2);
        }
    }

    static class SystemMetadata
    {
        public const string MaxAge = "$maxAge";
        public const string MaxCount = "$maxCount";
        public const string TruncateBefore = "$tb";
        public const string CacheControl = "$cacheControl";
        
        public const string Acl = "$acl";
        public const string AclRead = "$r";
        public const string AclWrite = "$w";
        public const string AclDelete = "$d";
        public const string AclMetaRead = "$mr";
        public const string AclMetaWrite = "$mw";

        public const string UserStreamAcl = "$userStreamAcl";
        public const string SystemStreamAcl = "$systemStreamAcl";
    }

    internal static class SystemEventTypes
    {
        public const string StreamDeleted = "$streamDeleted";
        public const string StatsCollection = "$statsCollected";
        public const string LinkTo = "$>";
        public const string StreamMetadata = "$metadata";
        public const string Settings = "$settings";
    }
}