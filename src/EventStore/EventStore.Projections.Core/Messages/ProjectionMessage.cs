// Copyright (c) 2012, Event Store LLP
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
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    public interface ICoreProjection : IHandle<ProjectionMessage.Projections.CommittedEventReceived>,
                                       IHandle<ProjectionMessage.Projections.CheckpointLoaded>,
                                       IHandle<ProjectionMessage.Projections.CheckpointSuggested>,
                                       IHandle<ProjectionMessage.Projections.ProgressChanged>,
                                       IHandle<ProjectionMessage.Projections.CheckpointCompleted>,
                                       IHandle<ProjectionMessage.Projections.PauseRequested>
    {
    }

    public static class ProjectionMessage
    {
        public abstract class ManagementMessage : Message
        {
            private readonly Guid _correlationId;

            protected ManagementMessage(Guid correlationId)
            {
                _correlationId = correlationId;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public static class CoreService
        {
            public class Start : Message
            {
            }

            public class Stop : Message
            {
            }

            public class Connected : Message
            {
                private readonly TcpConnectionManager _connection;

                public Connected(TcpConnectionManager connection)
                {
                    _connection = connection;
                }

                public TcpConnectionManager Connection
                {
                    get { return _connection; }
                }
            }

            public class Tick : Message
            {
                private readonly Action _action;

                public Tick(Action action)
                {
                    _action = action;
                }

                public Action Action
                {
                    get { return _action; }
                }
            }


            public static class Management
            {
                public class Create : ManagementMessage
                {
                    private readonly IEnvelope _envelope;
                    private readonly ProjectionConfig _config;
                    private readonly Func<IProjectionStateHandler> _handlerFactory;
                    private readonly string _name;

                    public Create(
                        IEnvelope envelope, Guid correlationId, string name, ProjectionConfig config,
                        Func<IProjectionStateHandler> handlerFactory)
                        : base(correlationId)
                    {
                        _envelope = envelope;
                        _name = name;
                        _config = config;
                        _handlerFactory = handlerFactory;
                    }

                    public ProjectionConfig Config
                    {
                        get { return _config; }
                    }

                    public Func<IProjectionStateHandler> HandlerFactory
                    {
                        get { return _handlerFactory; }
                    }

                    public string Name
                    {
                        get { return _name; }
                    }

                    public IEnvelope Envelope
                    {
                        get { return _envelope; }
                    }
                }

                public class Dispose : ManagementMessage
                {
                    public Dispose(Guid correlationId)
                        : base(correlationId)
                    {
                    }
                }
            }
        }

        public static class Projections
        {
            public static class Management
            {
                public class Start : ManagementMessage
                {
                    public Start(Guid correlationId)
                        : base(correlationId)
                    {
                    }
                }

                public class Stop : ManagementMessage
                {
                    public Stop(Guid correlationId)
                        : base(correlationId)
                    {
                    }
                }

                public class GetState : ManagementMessage
                {
                    private readonly IEnvelope _envelope;

                    public GetState(IEnvelope envelope, Guid correlationId)
                        : base(correlationId)
                    {
                        _envelope = envelope;
                    }

                    public IEnvelope Envelope
                    {
                        get { return _envelope; }
                    }
                }

                public class UpdateStatistics : ManagementMessage
                {
                    public UpdateStatistics(Guid correlationId)
                        : base(correlationId)
                    {
                    }
                }

                public class StateReport : ManagementMessage
                {
                    private readonly string _state;

                    public StateReport(Guid correlationId, string state)
                        : base(correlationId)
                    {
                        _state = state;
                    }

                    public string State
                    {
                        get { return _state; }
                    }
                }

                public class StatisticsReport : ManagementMessage
                {
                    private readonly ProjectionStatistics _statistics;

                    public StatisticsReport(Guid correlationId, ProjectionStatistics statistics)
                        : base(correlationId)
                    {
                        _statistics = statistics;
                    }

                    public ProjectionStatistics Statistics
                    {
                        get { return _statistics; }
                    }
                }
            }

            public class CheckpointLoaded : Message
            {
                private readonly Guid _correlationId;
                private readonly CheckpointTag _checkpointTag;
                private readonly string _checkpointData;

                public CheckpointLoaded(Guid correlationId, CheckpointTag checkpointTag, string checkpointData)
                {
                    _correlationId = correlationId;
                    _checkpointTag = checkpointTag;
                    _checkpointData = checkpointData;
                }

                public Guid CorrelationId
                {
                    get { return _correlationId; }
                }

                public CheckpointTag CheckpointTag
                {
                    get { return _checkpointTag; }
                }

                public string CheckpointData
                {
                    get { return _checkpointData; }
                }
            }

            /// <summary>
            /// A ChechpointSuggested message is sent to core projection 
            /// to allow bookmarking a position that can be used to 
            /// restore the projection processing (typically
            /// an event at this position does not satisfy projection filter)
            /// </summary>
            public class CheckpointSuggested : Message
            {
                private readonly Guid _correlationId;
                private readonly CheckpointTag _checkpointTag;
                private readonly float _progress;

                public CheckpointSuggested(Guid correlationId, CheckpointTag checkpointTag, float progress)
                {
                    _correlationId = correlationId;
                    _checkpointTag = checkpointTag;
                    _progress = progress;
                }

                public Guid CorrelationId
                {
                    get { return _correlationId; }
                }

                public CheckpointTag CheckpointTag
                {
                    get { return _checkpointTag; }
                }

                public float Progress
                {
                    get { return _progress; }
                }
            }

            public class ProgressChanged : Message
            {
                private readonly Guid _correlationId;
                private readonly CheckpointTag _checkpointTag;
                private readonly float _progress;

                public ProgressChanged(Guid correlationId, CheckpointTag checkpointTag, float progress)
                {
                    _correlationId = correlationId;
                    _checkpointTag = checkpointTag;
                    _progress = progress;
                }

                public Guid CorrelationId
                {
                    get { return _correlationId; }
                }

                public CheckpointTag CheckpointTag
                {
                    get { return _checkpointTag; }
                }

                public float Progress
                {
                    get { return _progress; }
                }            
            }

            public class CommittedEventReceived : Message
            {
                public static CommittedEventReceived Sample(
                    Guid correlationId, EventPosition position, string eventStreamId, int eventSequenceNumber,
                    bool resolvedLinkTo, Event data)
                {
                    return new CommittedEventReceived(
                        correlationId, position, eventStreamId, eventSequenceNumber, resolvedLinkTo, data, 77.7f);
                }

                private readonly Guid _correlationId;
                private readonly Event _data;
                private readonly string _eventStreamId;
                private readonly int _eventSequenceNumber;
                private readonly bool _resolvedLinkTo;
                private readonly string _positionStreamId;
                private readonly int _positionSequenceNumber;
                private readonly EventPosition _position;
                private readonly CheckpointTag _checkpointTag;
                private readonly float _progress;

                private CommittedEventReceived(
                    Guid correlationId, EventPosition position, CheckpointTag checkpointTag, string positionStreamId,
                    int positionSequenceNumber, string eventStreamId, int eventSequenceNumber, bool resolvedLinkTo,
                    Event data, float progress)
                {
                    if (data == null) throw new ArgumentNullException("data");
                    _correlationId = correlationId;
                    _data = data;
                    _progress = progress;
                    _position = position;
                    _checkpointTag = checkpointTag;
                    _positionStreamId = positionStreamId;
                    _positionSequenceNumber = positionSequenceNumber;
                    _eventStreamId = eventStreamId;
                    _eventSequenceNumber = eventSequenceNumber;
                    _resolvedLinkTo = resolvedLinkTo;
                }

                private CommittedEventReceived(
                    Guid correlationId, EventPosition position, string eventStreamId, int eventSequenceNumber,
                    bool resolvedLinkTo, Event data, float progress)
                    : this(
                        correlationId, position,
                        CheckpointTag.FromPosition(position.CommitPosition, position.PreparePosition), eventStreamId,
                        eventSequenceNumber, eventStreamId, eventSequenceNumber, resolvedLinkTo, data, progress)
                {
                }

                public Event Data
                {
                    get { return _data; }
                }

                public EventPosition Position
                {
                    get { return _position; }
                }

                public string EventStreamId
                {
                    get { return _eventStreamId; }
                }

                public Guid CorrelationId
                {
                    get { return _correlationId; }
                }

                public int EventSequenceNumber
                {
                    get { return _eventSequenceNumber; }
                }

                public string PositionStreamId
                {
                    get { return _positionStreamId; }
                }

                public int PositionSequenceNumber
                {
                    get { return _positionSequenceNumber; }
                }

                public bool ResolvedLinkTo
                {
                    get { return _resolvedLinkTo; }
                }

                public CheckpointTag CheckpointTag
                {
                    get { return _checkpointTag; }
                }

                public float Progress
                {
                    get { return _progress; }
                }

                public static CommittedEventReceived FromCommittedEventDistributed(
                    CommittedEventDistributed message, CheckpointTag checkpointTag)
                {
                    return new CommittedEventReceived(
                        message.CorrelationId, message.Position, checkpointTag, message.PositionStreamId,
                        message.PositionSequenceNumber, message.EventStreamId, message.EventSequenceNumber,
                        message.ResolvedLinkTo, message.Data, message.Progress);
                }
            }

            public class CommittedEventDistributed : Message
            {
                private readonly Guid _correlationId;
                private readonly Event _data;
                private readonly string _eventStreamId;
                private readonly int _eventSequenceNumber;
                private readonly bool _resolvedLinkTo;
                private readonly string _positionStreamId;
                private readonly int _positionSequenceNumber;
                private readonly EventPosition _position;
                private readonly long? _safeTransactionFileReaderJoinPosition;
                private readonly float _progress;

                //NOTE: committed event with null event _data means - end of the source reached.  
                // Current last available TF commit position is in _position.CommitPosition
                // TODO: separate message?

                public CommittedEventDistributed(
                    Guid correlationId, EventPosition position, string positionStreamId, int positionSequenceNumber,
                    string eventStreamId, int eventSequenceNumber, bool resolvedLinkTo, Event data,
                    long? safeTransactionFileReaderJoinPosition, float progress)
                {
                    _correlationId = correlationId;
                    _data = data;
                    _safeTransactionFileReaderJoinPosition = safeTransactionFileReaderJoinPosition;
                    _progress = progress;
                    _position = position;
                    _positionStreamId = positionStreamId;
                    _positionSequenceNumber = positionSequenceNumber;
                    _eventStreamId = eventStreamId;
                    _eventSequenceNumber = eventSequenceNumber;
                    _resolvedLinkTo = resolvedLinkTo;
                }

                public CommittedEventDistributed(
                    Guid correlationId, EventPosition position, string eventStreamId, int eventSequenceNumber,
                    bool resolvedLinkTo, Event data)
                    : this(
                        correlationId, position, eventStreamId, eventSequenceNumber, eventStreamId, eventSequenceNumber,
                        resolvedLinkTo, data, position.PreparePosition, 11.1f)
                {
                }

                public Event Data
                {
                    get { return _data; }
                }

                public EventPosition Position
                {
                    get { return _position; }
                }

                public string EventStreamId
                {
                    get { return _eventStreamId; }
                }

                public Guid CorrelationId
                {
                    get { return _correlationId; }
                }

                public int EventSequenceNumber
                {
                    get { return _eventSequenceNumber; }
                }

                public string PositionStreamId
                {
                    get { return _positionStreamId; }
                }

                public int PositionSequenceNumber
                {
                    get { return _positionSequenceNumber; }
                }

                public bool ResolvedLinkTo
                {
                    get { return _resolvedLinkTo; }
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

            public class SubscribeProjection : Message
            {
                private readonly Guid _correlationId;
                private readonly ICoreProjection _subscriber;
                private readonly CheckpointTag _fromPosition;
                private readonly CheckpointStrategy _checkpointStrategy;
                private readonly long _checkpointUnhandledBytesThreshold;

                public SubscribeProjection(
                    Guid correlationId, ICoreProjection subscriber, CheckpointTag from,
                    CheckpointStrategy checkpointStrategy, long checkpointUnhandledBytesThreshold)
                {
                    _correlationId = correlationId;
                    _subscriber = subscriber;
                    _fromPosition = from;
                    _checkpointStrategy = checkpointStrategy;
                    _checkpointUnhandledBytesThreshold = checkpointUnhandledBytesThreshold;
                }

                public ICoreProjection Subscriber
                {
                    get { return _subscriber; }
                }

                public CheckpointTag FromPosition
                {
                    get { return _fromPosition; }
                }

                public Guid CorrelationId
                {
                    get { return _correlationId; }
                }

                public CheckpointStrategy CheckpointStrategy
                {
                    get { return _checkpointStrategy; }
                }

                public long CheckpointUnhandledBytesThreshold
                {
                    get { return _checkpointUnhandledBytesThreshold; }
                }
            }

            public class ReadyForCheckpoint : Message
            {
            }

            public class CheckpointCompleted : Message
            {
                private readonly CheckpointTag _checkpointTag;

                public CheckpointCompleted(CheckpointTag checkpointTag)
                {
                    _checkpointTag = checkpointTag;
                }

                public CheckpointTag CheckpointTag
                {
                    get { return _checkpointTag; }
                }
            }

            public class PauseRequested : Message
            {
            }

            public class PauseProjectionSubscription : Message
            {
                private readonly Guid _correlationId;

                public PauseProjectionSubscription(Guid correlationId)
                {
                    _correlationId = correlationId;
                }

                public Guid CorrelationId
                {
                    get { return _correlationId; }
                }
            }

            public class ResumeProjectionSubscription : Message
            {
                private readonly Guid _correlationId;

                public ResumeProjectionSubscription(Guid correlationId)
                {
                    _correlationId = correlationId;
                }

                public Guid CorrelationId
                {
                    get { return _correlationId; }
                }
            }

            public class UnsubscribeProjection : Message
            {
                private readonly Guid _correlationId;

                public UnsubscribeProjection(Guid correlationId)
                {
                    _correlationId = correlationId;
                }

                public Guid CorrelationId
                {
                    get { return _correlationId; }
                }
            }

            public static class StatusReport
            {
                public class Stopped : Message
                {
                    private readonly Guid _correlationId;

                    public Stopped(Guid correlationId)
                    {
                        _correlationId = correlationId;
                    }

                    public Guid CorrelationId
                    {
                        get { return _correlationId; }
                    }
                }

                public class Started : Message
                {
                    private readonly Guid _correlationId;

                    public Started(Guid correlationId)
                    {
                        _correlationId = correlationId;
                    }

                    public Guid CorrelationId
                    {
                        get { return _correlationId; }
                    }
                }

                public class Faulted : Message
                {
                    private readonly Guid _correlationId;
                    private readonly string _faultedReason;

                    public Faulted(Guid correlationId, string faultedReason)
                    {
                        _correlationId = correlationId;
                        _faultedReason = faultedReason;
                    }

                    public Guid CorrelationId
                    {
                        get { return _correlationId; }
                    }

                    public string FaultedReason
                    {
                        get { return _faultedReason; }
                    }
                }
            }
        }
    }
}
