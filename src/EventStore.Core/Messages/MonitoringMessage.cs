using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http.Controllers;

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
            public readonly bool Success;
            public readonly List<SubscriptionInfo> SubscriptionStats;
            public string ErrorString;

            public GetPersistentSubscriptionStatsCompleted(OperationStatus result, List<SubscriptionInfo> subscriptionStats, string errorString="")
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
        }

        public class ConnectionInfo
        {
            public string From { get; set; }
            public string Username { get; set; }
            public int AverageItemsPerSecond { get; set; }
            public long TotalItems { get; set; }
            public long CountSinceLastMeasurement { get; set; }
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
