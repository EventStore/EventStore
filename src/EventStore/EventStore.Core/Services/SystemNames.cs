﻿// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  

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
    }

    public static class SystemStreams
    {
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
        public const string StreamDeleted = "$streamDeleted";
        public const string StatsCollection = "$statsCollected";
        public const string LinkTo = "$>";
        public const string StreamReference = "$@";
        public const string StreamMetadata = "$metadata";
        public const string Settings = "$settings";

        public const string V2__StreamCreated_InIndex = "StreamCreated";
        public const string V1__StreamCreated__ = "$stream-created";
        public const string V1__StreamCreatedImplicit__ = "$stream-created-implicit";

        public static string StreamReferenceEventToStreamId(string eventType, byte[] data)
        {
            string streamId = null;
            switch (eventType)
            {
                case LinkTo:
                {
                    string[] parts = Helper.UTF8NoBom.GetString(data).Split('@');
                    streamId = parts[1];
                    break;
                }
                case StreamReference:
                case V1__StreamCreated__:
                case V2__StreamCreated_InIndex:
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
            string streamId = null;
            switch (eventType)
            {
                case LinkTo:
                    {
                        string[] parts = data.Split('@');
                        streamId = parts[1];
                        break;
                    }
                case StreamReference:
                case V1__StreamCreated__:
                case V2__StreamCreated_InIndex:
                    {
                        streamId = data;
                        break;
                    }
                default:
                    throw new NotSupportedException("Unknown event type: " + eventType);
            }
            return streamId;
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
