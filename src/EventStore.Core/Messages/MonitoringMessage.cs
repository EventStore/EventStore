﻿using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;
using EventStore.Core.Services.PersistentSubscription;

namespace EventStore.Core.Messages
{
    public static class MonitoringMessage
    {
        public class GetAllPersistentSubscriptionStats : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IEnvelope Envelope;

            public GetAllPersistentSubscriptionStats(IEnvelope envelope)
            {
                Ensure.NotNull(envelope, "envelope");
                Envelope = envelope;
            }
        }

        public class GetPersistentSubscriptionStats : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public string EventStreamId { get { return _eventStreamId; } }
            public string GroupName { get { return _groupName; } }

            public readonly IEnvelope Envelope;
            private readonly string _eventStreamId;
            private readonly string _groupName;

            public GetPersistentSubscriptionStats(IEnvelope envelope, string eventStreamId, string groupName)
            {
                Ensure.NotNull(envelope, "envelope");
                Envelope = envelope;
                _eventStreamId = eventStreamId;
                _groupName = groupName;
            }
        }

        public class GetStreamPersistentSubscriptionStats : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public string EventStreamId { get { return _eventStreamId; } }

            public readonly IEnvelope Envelope;
            private readonly string _eventStreamId;

            public GetStreamPersistentSubscriptionStats(IEnvelope envelope, string eventStreamId)
            {
                Ensure.NotNull(envelope, "envelope");
                Envelope = envelope;
                _eventStreamId = eventStreamId;
            }
        }

        public class GetPersistentSubscriptionStatsCompleted : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly OperationStatus Result;
            public readonly List<SubscriptionInfo> SubscriptionStats;
            public string ErrorString;

            public GetPersistentSubscriptionStatsCompleted(OperationStatus result, List<SubscriptionInfo> subscriptionStats, string errorString = "")
            {
                Result = result;
                SubscriptionStats = subscriptionStats;
                ErrorString = errorString;
            }

            public enum OperationStatus
            {
                Success = 0,
                NotFound = 1,
                Fail = 2,
                NotReady = 3
            }
        }

        public class SubscriptionInfo
        {
            public string EventStreamId { get; set; }
            public string GroupName { get; set; }
            public string Status { get; set; }
            public List<ConnectionInfo> Connections { get; set; }
            public int AveragePerSecond { get; set; }
            public long TotalItems { get; set; }
            public long CountSinceLastMeasurement { get; set; }
            public long LastProcessedEventNumber { get; set; }
            public long LastKnownMessage { get; set; }
            public bool ResolveLinktos { get; set; }
            public long StartFrom { get; set; }
            public int MessageTimeoutMilliseconds { get; set; }
            public bool ExtraStatistics { get; set; }
            public int MaxRetryCount { get; set; }
            public int LiveBufferSize { get; set; }
            public int BufferSize { get; set; }
            public int ReadBatchSize { get; set; }
            public int CheckPointAfterMilliseconds { get; set; }
            public int MinCheckPointCount { get; set; }
            public int MaxCheckPointCount { get; set; }
            public int ReadBufferCount { get; set; }
            public long LiveBufferCount { get; set; }
            public int RetryBufferCount { get; set; }
            public int TotalInFlightMessages { get; set; }
            public string NamedConsumerStrategy { get; set; }
            public int MaxSubscriberCount { get; set; }
        }

        public class ConnectionInfo
        {
            public string From { get; set; }
            public string Username { get; set; }
            public int AverageItemsPerSecond { get; set; }
            public long TotalItems { get; set; }
            public long CountSinceLastMeasurement { get; set; }
            public List<Measurement> ObservedMeasurements { get; set; }
            public int AvailableSlots { get; set; }
            public int InFlightMessages { get; set; }
        }

        public class GetFreshStats : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IEnvelope Envelope;
            public readonly Func<Dictionary<string, object>, Dictionary<string, object>> StatsSelector;
            public readonly bool UseMetadata;
            public readonly bool UseGrouping;

            public GetFreshStats(IEnvelope envelope,
                                 Func<Dictionary<string, object>, Dictionary<string, object>> statsSelector,
                                 bool useMetadata,
                                 bool useGrouping)
            {
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(statsSelector, "statsSelector");

                Envelope = envelope;
                StatsSelector = statsSelector;
                UseMetadata = useMetadata;
                UseGrouping = useGrouping;
            }
        }

        public class GetFreshStatsCompleted : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly bool Success;
            public readonly Dictionary<string, object> Stats;

            public GetFreshStatsCompleted(bool success, Dictionary<string, object> stats)
            {
                Success = success;
                Stats = stats;
            }
        }

        public class GetFreshTcpConnectionStats : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IEnvelope Envelope;

            public GetFreshTcpConnectionStats(IEnvelope envelope)
            {
                Ensure.NotNull(envelope, "envelope");

                Envelope = envelope;
            }
        }

        public class GetFreshTcpConnectionStatsCompleted : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly List<TcpConnectionStats> ConnectionStats;

            public GetFreshTcpConnectionStatsCompleted(List<TcpConnectionStats> connectionStats)
            {
                ConnectionStats = connectionStats;
            }
        }

        public class TcpConnectionStats
        {
            public string RemoteEndPoint { get; set; }
            public string LocalEndPoint { get; set; }
            public Guid ConnectionId { get; set; }
            public long TotalBytesSent { get; set; }
            public long TotalBytesReceived { get; set; }
            public int PendingSendBytes { get; set; }
            public int PendingReceivedBytes { get; set; }
            public bool IsExternalConnection { get; set; }
        }

        public class InternalStatsRequest : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IEnvelope Envelope;

            public InternalStatsRequest(IEnvelope envelope)
            {
                Ensure.NotNull(envelope, "envelope");

                Envelope = envelope;
            }
        }

        public class InternalStatsRequestResponse : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Dictionary<string, object> Stats;

            public InternalStatsRequestResponse(Dictionary<string, object> stats)
            {
                Ensure.NotNull(stats, "stats");

                Stats = stats;
            }
        }
    }
}
