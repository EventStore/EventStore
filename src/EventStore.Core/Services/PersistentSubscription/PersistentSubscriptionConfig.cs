﻿using System;
using System.Collections.Generic;
using EventStore.Common.Utils;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionConfig
    {
        public string Version;
        public DateTime Updated;
        public string UpdatedBy;
        public List<PersistentSubscriptionEntry> Entries = new List<PersistentSubscriptionEntry>();

        public byte[] GetSerializedForm()
        {
            return this.ToJsonBytes();
        }

        public static PersistentSubscriptionConfig FromSerializedForm(byte[] data)
        {
            try
            {
                var ret = data.ParseJson<PersistentSubscriptionConfig>();
                if(ret.Version == null) throw new BadConfigDataException("Deserialized but no version present, invalid configuration data.", null);
                return ret;
            }
            catch (Exception ex)
            {
                throw new BadConfigDataException("The config data appears to be invalid", ex);
            }
        }
    }

    public class BadConfigDataException : Exception
    {
        public BadConfigDataException(string message, Exception inner) : base(message, inner)
        {
            
        }
    }

    public class PersistentSubscriptionEntry
    {
        public string Stream;
        public string Group;
        public bool ResolveLinkTos;
        public bool ExtraStatistics;
        public int MessageTimeout;
        public int StartFrom;
        public int LiveBufferSize;
        public int HistoryBufferSize;
        public int MaxRetryCount;
        public int ReadBatchSize;
        public bool PreferRoundRobin;
        public int CheckPointAfter { get; set; }
        public int MinCheckPointCount { get; set; }
        public int MaxCheckPointCount { get; set; }
    }
}