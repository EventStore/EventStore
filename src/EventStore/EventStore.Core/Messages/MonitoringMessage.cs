using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages
{
    public static class MonitoringMessage
    {
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
