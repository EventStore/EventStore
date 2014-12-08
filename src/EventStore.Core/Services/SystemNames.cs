using System;
using EventStore.Common.Utils;

namespace EventStore.Core.Services
{
    public static class SystemHeaders
    {
        public const string ExpectedVersion = "ES-ExpectedVersion";
        public const string RequireMaster = "ES-RequireMaster";
        public const string ResolveLinkTos = "ES-ResolveLinkTos";
        public const string LongPoll = "ES-LongPoll";
        public const string TrustedAuth = "ES-TrustedAuth";
        public const string ProjectionPosition = "ES-Position";
        public const string HardDelete = "ES-HardDelete";
        public const string EventId = "ES-EventId";
        public const string EventType = "ES-EventType";
    }

    public static class SystemStreams
    {
        public const string PersistentSubscriptionConfig = "$persistentSubscriptionConfig";
        public const string AllStream = "$all";
        public const string StreamsStream = "$streams";
        public const string SettingsStream = "$settings";
        public const string StatsStreamPrefix = "$stats";

        public static bool IsSystemStream(string streamId)
        {
            return streamId.Length != 0 && streamId[0] == '$';
        }

        public static string MetastreamOf(string streamId)
        {
            return "$$" + streamId;
        }

        public static bool IsMetastream(string streamId)
        {
            return streamId.Length >= 2 && streamId[0] == '$' && streamId[1] == '$';
        }

        public static string OriginalStreamOf(string metastreamId)
        {
            return metastreamId.Substring(2);
        }
    }

    public static class SystemMetadata
    {
        public const string MaxAge = "$maxAge";
        public const string MaxCount = "$maxCount";
        public const string TruncateBefore = "$tb";
        public const string TempStream = "$tmp";
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

    public static class SystemEventTypes
    {
        private static readonly char[] LinkToSeparator = {'@'};
        public const string StreamDeleted = "$streamDeleted";
        public const string StatsCollection = "$statsCollected";
        public const string LinkTo = "$>";
        public const string StreamReference = "$@";
        public const string StreamMetadata = "$metadata";
        public const string Settings = "$settings";

        public const string V2StreamCreatedInIndex = "StreamCreated";
        public const string V1StreamCreated = "$stream-created";
        public const string V1StreamCreatedImplicit = "$stream-created-implicit";

        public static string StreamReferenceEventToStreamId(string eventType, byte[] data)
        {
            string streamId;
            switch (eventType)
            {
                case LinkTo:
                {
                    string[] parts = Helper.UTF8NoBom.GetString(data).Split(LinkToSeparator, 2);
                    streamId = parts[1];
                    break;
                }
                case StreamReference:
                case V1StreamCreated:
                case V2StreamCreatedInIndex:
                {
                    streamId = Helper.UTF8NoBom.GetString(data);
                    break;
                }
                default:
                    throw new NotSupportedException("Unknown event type: " + eventType);
            }
            return streamId;
        }

        public static string StreamReferenceEventToStreamId(string eventType, string data)
        {
            string streamId;
            switch (eventType)
            {
                case LinkTo:
                    {
                        string[] parts = data.Split(LinkToSeparator, 2);
                        streamId = parts[1];
                        break;
                    }
                case StreamReference:
                case V1StreamCreated:
                case V2StreamCreatedInIndex:
                    {
                        streamId = data;
                        break;
                    }
                default:
                    throw new NotSupportedException("Unknown event type: " + eventType);
            }
            return streamId;
        }

        public static int EventLinkToEventNumber(string link)
        {
            var parts = link.Split(LinkToSeparator, 2);
            return int.Parse(parts[0]);
        }
    }

    public static class SystemUsers
    {
        public const string Admin = "admin";
        public const string DefaultAdminPassword = "changeit";
    }

    public static class SystemRoles
    {
        public const string Admins = "$admins";
        public const string All = "$all";
    }
}
