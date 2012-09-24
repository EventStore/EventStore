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
using System.Diagnostics;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services
{
    public class TwoPhaseCommitRequestManager : IHandle<ReplicationMessage.CreateStreamRequestCreated>, 
                                                IHandle<ReplicationMessage.WriteRequestCreated>,
                                                IHandle<ReplicationMessage.TransactionCommitRequestCreated>,
                                                IHandle<ReplicationMessage.DeleteStreamRequestCreated>,
                                                IHandle<ReplicationMessage.AlreadyCommitted>,
                                                IHandle<ReplicationMessage.PrepareAck>,
                                                IHandle<ReplicationMessage.CommitAck>,
                                                IHandle<ReplicationMessage.WrongExpectedVersion>,
                                                IHandle<ReplicationMessage.StreamDeleted>,
                                                IHandle<ReplicationMessage.PreparePhaseTimeout>,
                                                IHandle<ReplicationMessage.CommitPhaseTimeout>
    {
        public static readonly TimeSpan PrepareTimeout = TimeSpan.FromMilliseconds(2000);
        public static readonly TimeSpan CommitTimeout = TimeSpan.FromMilliseconds(2000);

        private readonly IPublisher _bus;
        private readonly IEnvelope _publishEnvelope;

        private IEnvelope _responseEnvelope;
        private Guid _correlationId;
        private string _eventStreamId;

        private int _awaitingPrepare;
        private int _awaitingCommit;

        private long _preparePos = -1;

        private bool _completed;
        private bool _initialized;

        private RequestType _requestType;

        public TwoPhaseCommitRequestManager(IPublisher bus, int prepareCount, int commitCount)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.Positive(prepareCount, "prepareCount");
            Ensure.Positive(commitCount, "commitCount");

            _bus = bus;
            _awaitingPrepare = prepareCount;
            _awaitingCommit = commitCount;

            _publishEnvelope = new PublishEnvelope(_bus);
        }

        public void Handle(ReplicationMessage.CreateStreamRequestCreated request)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;
            _requestType = RequestType.CreateStream;
            _responseEnvelope = request.Envelope;
            _correlationId = request.CorrelationId;
            _eventStreamId = request.EventStreamId;

            _bus.Publish(new ReplicationMessage.WritePrepares(
                request.CorrelationId,
                _publishEnvelope,
                request.EventStreamId, 
                ExpectedVersion.NoStream,
                new[] { new Event(Guid.NewGuid(), "StreamCreated", true, LogRecord.NoData, request.Metadata) },
                allowImplicitStreamCreation: false, 
                liveUntil: DateTime.UtcNow.AddSeconds(PrepareTimeout.Seconds)));
            _bus.Publish(TimerMessage.Schedule.Create(PrepareTimeout,
                                                      _publishEnvelope,
                                                      new ReplicationMessage.PreparePhaseTimeout(_correlationId)));
        }

        public void Handle(ReplicationMessage.WriteRequestCreated request)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;
            _requestType = RequestType.Write;
            _responseEnvelope = request.Envelope;
            _correlationId = request.CorrelationId;
            _eventStreamId = request.EventStreamId;

            _bus.Publish(new ReplicationMessage.WritePrepares(request.CorrelationId,
                                                              _publishEnvelope,
                                                              request.EventStreamId,
                                                              request.ExpectedVersion,
                                                              request.Events,
                                                              allowImplicitStreamCreation: true,
                                                              liveUntil: DateTime.UtcNow.AddSeconds(PrepareTimeout.Seconds)));
            _bus.Publish(TimerMessage.Schedule.Create(PrepareTimeout,
                                                      _publishEnvelope,
                                                      new ReplicationMessage.PreparePhaseTimeout(_correlationId)));
        }

        public void Handle(ReplicationMessage.TransactionCommitRequestCreated request)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;
            _requestType = RequestType.TransactionCommit;
            _responseEnvelope = request.Envelope;
            _correlationId = request.CorrelationId;
            _preparePos = request.TransactionId;

            _bus.Publish(new ReplicationMessage.WriteTransactionPrepare(request.CorrelationId, _publishEnvelope, request.TransactionId, request.EventStreamId));
            _bus.Publish(TimerMessage.Schedule.Create(PrepareTimeout, _publishEnvelope, new ReplicationMessage.PreparePhaseTimeout(_correlationId)));
        }

        public void Handle(ReplicationMessage.DeleteStreamRequestCreated request)
        {
            if (_initialized)
                throw new InvalidOperationException();

            _initialized = true;
            _requestType = RequestType.DeleteStream;
            _responseEnvelope = request.Envelope;
            _correlationId = request.CorrelationId;
            _eventStreamId = request.EventStreamId;

            _bus.Publish(new ReplicationMessage.WriteDelete(request.CorrelationId,
                                                            _publishEnvelope,
                                                            request.EventStreamId,
                                                            request.ExpectedVersion,
                                                            allowImplicitStreamCreation: true,
                                                            liveUntil: DateTime.UtcNow.AddSeconds(PrepareTimeout.Seconds)));
            _bus.Publish(TimerMessage.Schedule.Create(PrepareTimeout,
                                                      _publishEnvelope,
                                                      new ReplicationMessage.PreparePhaseTimeout(_correlationId)));
        }

        public void Handle(ReplicationMessage.AlreadyCommitted message)
        {
            Debug.Assert(message.EventStreamId == _eventStreamId && message.CorrelationId == _correlationId);
            CompleteSuccessRequest(_correlationId, _eventStreamId, message.StartEventNumber);
        }

        public void Handle(ReplicationMessage.PrepareAck message)
        {
            if (_completed)
                return;

            if ((message.Flags & PrepareFlags.TransactionBegin) != 0)
                _preparePos = message.LogPosition;

            if ((message.Flags & PrepareFlags.TransactionEnd) != 0)
            {
                _awaitingPrepare -= 1;
                if (_awaitingPrepare == 0)
                {
                    _bus.Publish(new ReplicationMessage.WriteCommit(message.CorrelationId, _publishEnvelope, _preparePos));
                    _bus.Publish(TimerMessage.Schedule.Create(CommitTimeout,
                                                              _publishEnvelope,
                                                              new ReplicationMessage.CommitPhaseTimeout(_correlationId)));
                }
            }
        }

        public void Handle(ReplicationMessage.CommitAck message)
        {
            if (_completed)
                return;

            _awaitingCommit -= 1;
            if (_awaitingCommit == 0)
                CompleteSuccessRequest(message.CorrelationId, _eventStreamId, message.EventNumber);
        }

        public void Handle(ReplicationMessage.WrongExpectedVersion message)
        {
            if (_completed)
                return;

            CompleteFailedRequest(message.CorrelationId, _eventStreamId, OperationErrorCode.WrongExpectedVersion, "Wrong expected version.");
        }

        public void Handle(ReplicationMessage.StreamDeleted message)
        {
            if (_completed)
                return;

            CompleteFailedRequest(message.CorrelationId, _eventStreamId, OperationErrorCode.StreamDeleted, "Stream is deleted.");
        }

        public void Handle(ReplicationMessage.PreparePhaseTimeout message)
        {
            if (_completed || _awaitingPrepare == 0)
                return;

            CompleteFailedRequest(message.CorrelationId, _eventStreamId, OperationErrorCode.PrepareTimeout, "Prepare phase timeout.");
        }

        public void Handle(ReplicationMessage.CommitPhaseTimeout message)
        {
            if (_completed || _awaitingCommit == 0)
                return;

            CompleteFailedRequest(message.CorrelationId, _eventStreamId, OperationErrorCode.CommitTimeout, "Commit phase timeout.");
        }

        private void CompleteSuccessRequest(Guid correlationId, string eventStreamId, int startEventNumber)
        {
            _completed = true;
            _bus.Publish(new ReplicationMessage.RequestCompleted(correlationId, true));
            switch (_requestType)
            {
                case RequestType.CreateStream:
                {
                    var responseMsg = new ClientMessage.CreateStreamCompleted(
                            correlationId, eventStreamId, OperationErrorCode.Success, null);
                    _responseEnvelope.ReplyWith(responseMsg);
                    break;
                }
                case RequestType.Write:
                {
                    var responseMsg = new ClientMessage.WriteEventsCompleted(
                            correlationId, eventStreamId, startEventNumber);
                    _responseEnvelope.ReplyWith(responseMsg);
                    break;
                }
                case RequestType.DeleteStream:
                {
                    var responseMsg = new ClientMessage.DeleteStreamCompleted(
                            correlationId, eventStreamId, OperationErrorCode.Success, null);
                    _responseEnvelope.ReplyWith(responseMsg);
                    break;
                }
                case RequestType.TransactionCommit:
                {
                    var responseMsg = new ClientMessage.TransactionCommitCompleted(
                            correlationId, _preparePos, OperationErrorCode.Success, null);
                    _responseEnvelope.ReplyWith(responseMsg);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void CompleteFailedRequest(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error)
        {
            Debug.Assert(errorCode != OperationErrorCode.Success);

            _completed = true;

            _bus.Publish(new ReplicationMessage.RequestCompleted(correlationId, false));

            switch (_requestType)
            {
                case RequestType.CreateStream:
                    {
                        var responseMsg = new ClientMessage.CreateStreamCompleted(correlationId, eventStreamId, errorCode, error);
                        _responseEnvelope.ReplyWith(responseMsg);
                        break;
                    }
                case RequestType.Write:
                    {
                        var responseMsg = new ClientMessage.WriteEventsCompleted(
                            correlationId, eventStreamId, errorCode, error);
                        _responseEnvelope.ReplyWith(responseMsg);
                        break;
                    }
                case RequestType.DeleteStream:
                    {
                        var responseMsg = new ClientMessage.DeleteStreamCompleted(
                            correlationId, eventStreamId, errorCode, error);
                        _responseEnvelope.ReplyWith(responseMsg);
                        break;
                    }
                case RequestType.TransactionCommit:
                    {
                        var responseMsg = new ClientMessage.TransactionCommitCompleted(
                            correlationId, _preparePos, errorCode, error);
                        _responseEnvelope.ReplyWith(responseMsg);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private enum RequestType
        {
            CreateStream,
            Write,
            TransactionCommit,
            DeleteStream
        }
    }
}
