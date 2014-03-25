using System;
using System.Security.Principal;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages.EventReaders.Feeds
{
    public abstract class FeedReaderMessage : Message
    {
        private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }

        public sealed class ReadPage: FeedReaderMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly IPrincipal User;

            public readonly QuerySourcesDefinition QuerySource;
            public readonly CheckpointTag FromPosition;
            public readonly int MaxEvents;

            public ReadPage(
                Guid correlationId, IEnvelope envelope, IPrincipal user, QuerySourcesDefinition querySource, CheckpointTag fromPosition,
                int maxEvents)
            {
                User = user;
                CorrelationId = correlationId;
                Envelope = envelope;
                QuerySource = querySource;
                FromPosition = fromPosition;
                MaxEvents = maxEvents;
            }
        }

        public sealed class FeedPage: FeedReaderMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public enum ErrorStatus
            {
                Success,
                NotAuthorized
            }

            public readonly Guid CorrelationId;
            public readonly ErrorStatus Error;
            public readonly TaggedResolvedEvent[] Events;
            public readonly CheckpointTag LastReaderPosition;

            public FeedPage(
                Guid correlationId, ErrorStatus error, TaggedResolvedEvent[] events, CheckpointTag lastReaderPosition)
            {
                CorrelationId = correlationId;
                Error = error;
                Events = events;
                LastReaderPosition = lastReaderPosition;
            }
        }
    }
}
