using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages
{
    public static class PluginMessage
    {
        public class GetStats : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IEnvelope Envelope;

            public GetStats(IEnvelope envelope)
            {
                Ensure.NotNull(envelope, "envelope");
                Envelope = envelope;
            }
        }

        public class GetStatsCompleted : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly OperationStatus Result;
            public readonly string Stats;
            public string ErrorString;

            public GetStatsCompleted(OperationStatus result, string stats, string errorString = "")
            {
                Result = result;
                Stats = stats;
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
    }
}