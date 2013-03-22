using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    public static partial class ProjectionCoreServiceMessage
    {
        public class StartReader : Message
        {
        }

        public class StopReader : Message
        {
        }

        public class ReaderTick : Message
        {
            private readonly Action _action;

            public ReaderTick(Action action)
            {
                _action = action;
            }

            public Action Action
            {
                get { return _action; }
            }
        }

        public class EventReaderIdle : Message
        {
            private readonly Guid _correlationId;
            private readonly DateTime _idleTimestampUtc;

            public EventReaderIdle(Guid correlationId, DateTime idleTimestampUtc)
            {
                _correlationId = correlationId;
                _idleTimestampUtc = idleTimestampUtc;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

            public DateTime IdleTimestampUtc
            {
                get { return _idleTimestampUtc; }
            }
        }

        public class EventReaderEof : Message
        {
            private readonly Guid _correlationId;

            public EventReaderEof(Guid correlationId)
            {
                _correlationId = correlationId;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class CommittedEventDistributed : Message
        {
            public static CommittedEventDistributed Sample(
                Guid correlationId, EventPosition position, string positionStreamId, int positionSequenceNumber,
                string eventStreamId, int eventSequenceNumber, bool resolvedLinkTo, Guid eventId, string eventType,
                bool isJson, byte[] data, byte[] metadata, long? safeTransactionFileReaderJoinPosition, float progress)
            {
                return new CommittedEventDistributed(
                    correlationId,
                    new ResolvedEvent(
                        positionStreamId, positionSequenceNumber, eventStreamId, eventSequenceNumber, resolvedLinkTo,
                        position, eventId, eventType, isJson, data, metadata, null, default(DateTime)),
                    safeTransactionFileReaderJoinPosition, progress);
            }

            public static CommittedEventDistributed Sample(
                Guid correlationId, EventPosition position, string eventStreamId, int eventSequenceNumber,
                bool resolvedLinkTo, Guid eventId, string eventType, bool isJson, byte[] data, byte[] metadata,
                DateTime? timestamp = null)
            {
                return new CommittedEventDistributed(
                    correlationId,
                    new ResolvedEvent(
                        eventStreamId, eventSequenceNumber, eventStreamId, eventSequenceNumber, resolvedLinkTo, position,
                        eventId, eventType, isJson, data, metadata, null, timestamp.GetValueOrDefault()),
                    position.PreparePosition, 11.1f);
            }

            private readonly Guid _correlationId;

            private readonly ResolvedEvent _data;

            private readonly long? _safeTransactionFileReaderJoinPosition;
            private readonly float _progress;

            //NOTE: committed event with null event _data means - end of the source reached.  
            // Current last available TF commit position is in _position.CommitPosition
            // TODO: separate message?

            public CommittedEventDistributed(
                Guid correlationId, ResolvedEvent data, long? safeTransactionFileReaderJoinPosition, float progress)
            {
                _correlationId = correlationId;
                _data = data;
                _safeTransactionFileReaderJoinPosition = safeTransactionFileReaderJoinPosition;
                _progress = progress;
            }

            public CommittedEventDistributed(Guid correlationId, ResolvedEvent data)
                : this(correlationId, data, data.Position.PreparePosition, 11.1f)
            {
            }

            public ResolvedEvent Data
            {
                get { return _data; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

            public long? SafeTransactionFileReaderJoinPosition
            {
                get { return _safeTransactionFileReaderJoinPosition; }
            }

            public float Progress
            {
                get { return _progress; }
            }
        }
    }
}