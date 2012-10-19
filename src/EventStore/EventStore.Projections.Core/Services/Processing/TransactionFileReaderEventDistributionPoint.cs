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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class TransactionFileReaderEventDistributionPoint : EventDistributionPoint
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<TransactionFileReaderEventDistributionPoint>();

        private bool _paused = true;
        private bool _pauseRequested = true;
        private bool _eventsRequested = false;
        private int _maxReadCount = 100;
        private bool _disposed;
        private EventPosition _from;

        public TransactionFileReaderEventDistributionPoint(
            IPublisher publisher, Guid distibutionPointCorrelationId, EventPosition from)
            : base(publisher, distibutionPointCorrelationId)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            _from = @from;
        }

        public override void Resume()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (!_pauseRequested)
                throw new InvalidOperationException("Is not paused");
            if (!_paused)
            {
                _pauseRequested = false;
                return;
            }
            
            _paused = false;
            _pauseRequested = false;
            _logger.Trace("Resuming event distribution {0} at '{1}'", _distibutionPointCorrelationId, _from);
            RequestEvents();
        }

        private void RequestEvents()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_eventsRequested)
                throw new InvalidOperationException("Read operation is already in progress");
            if (_pauseRequested || _paused)
                throw new InvalidOperationException("Paused or pause requested");
            _eventsRequested = true;

            _publisher.Publish(
                new ClientMessage.ReadAllEventsForward(
                    _distibutionPointCorrelationId, new SendToThisEnvelope(this), _from.CommitPosition,
                    _from.PreparePosition == -1 ? _from.CommitPosition : _from.PreparePosition, _maxReadCount, true));
        }

        public override void Pause()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_pauseRequested)
                throw new InvalidOperationException("Pause has been already requested");
            _pauseRequested = true;
            if (!_eventsRequested)
                _paused = true;
            _logger.Trace("Pausing event distribution {0} at '{1}", _distibutionPointCorrelationId, _from);
        }

        public override void Handle(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
        }

        public override void Handle(ClientMessage.ReadAllEventsForwardCompleted message)
        {
            if (_disposed)
                return;
            if (!_eventsRequested)
                throw new InvalidOperationException("Read events has not been requested");
            if (_paused)
                throw new InvalidOperationException("Paused");
            _eventsRequested = false;

            if (message.Result.Records.Count == 0)
            {
                // the end
                DeliverLastCommitPosition(_from.CommitPosition);
                // allow joining heading distribution
            }
            else
            {
                for (int index = 0; index < message.Result.Records.Count; index++)
                {
                    var @event = message.Result.Records[index];
                    DeliverEvent(@event);
                }
                _from = message.Result.NextPos;
            }
            if (_pauseRequested)
                _paused = true;
            else
                //TODO: we may publish this message somewhere 10 events before the end of the chunk
                _publisher.Publish(
                    new ProjectionMessage.CoreService.Tick(
                        () => { if (!_paused && !_disposed) RequestEvents(); }));
        }

        public override void Dispose()
        {
            _disposed = true;
        }

        private void DeliverLastCommitPosition(long lastCommitPosition)
        {
            _publisher.Publish(
                new ProjectionMessage.Projections.CommittedEventReceived(
                    _distibutionPointCorrelationId, new EventPosition(long.MinValue, lastCommitPosition), null, int.MinValue,
                    null, int.MinValue, false, null));
        }

        private void DeliverEvent(ResolvedEventRecord @event)
        {
            EventRecord positionEvent = (@event.Link ?? @event.Event);
            var receivedPosition = new EventPosition(@event.CommitPosition, positionEvent.LogPosition);
            if (_from > receivedPosition)
                throw new Exception(
                    string.Format(
                        "ReadFromTF returned events in incorrect order.  Last known position is: {0}.  Received position is: {1}",
                        _from, receivedPosition));

            _publisher.Publish(
                new ProjectionMessage.Projections.CommittedEventReceived(
                    _distibutionPointCorrelationId, receivedPosition, positionEvent.EventStreamId,
                    positionEvent.EventNumber, @event.Event.EventStreamId, @event.Event.EventNumber, @event.Link != null,
                    new Event(
                        @event.Event.EventId, @event.Event.EventType,
                        (@event.Event.Flags & PrepareFlags.IsJson) != 0, @event.Event.Data, @event.Event.Metadata)));
        }
    }
}
