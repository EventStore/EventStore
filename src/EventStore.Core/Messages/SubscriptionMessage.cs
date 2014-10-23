using System;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages
{
    public static class SubscriptionMessage
    {
        public class PollStream : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string StreamId;
            public readonly long LastCommitPosition;
            public readonly int? LastEventNumber;
            public readonly DateTime ExpireAt;

            public readonly Message OriginalRequest;

            public PollStream(string streamId, long lastCommitPosition, int? lastEventNumber, DateTime expireAt, Message originalRequest)
            {
                StreamId = streamId;
                LastCommitPosition = lastCommitPosition;
                LastEventNumber = lastEventNumber;
                ExpireAt = expireAt;
                OriginalRequest = originalRequest;
            }
        }

        public class CheckPollTimeout: Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }
        }


        public class PersistentSubscriptionTimerTick : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }
        }

        public class ReplayAllParkedMessages : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }
            public readonly string EventStreamId;
            public readonly string GroupName;

            public ReplayAllParkedMessages(string eventStreamId, string groupName)
            {
                EventStreamId = eventStreamId;
                GroupName = groupName;
            }
        }

        public class ReplayParkedMessage : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }
            public readonly string EventStreamId;
            public readonly string GroupName;
            public readonly ResolvedEvent Event;

            public ReplayParkedMessage(string streamId, string groupName, ResolvedEvent @event)
            {
                EventStreamId = streamId;
                GroupName = groupName;
                Event = @event;
            }
        }
    }
}