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
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class StreamReaderEventDistributionPoint : EventDistributionPoint
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<StreamReaderEventDistributionPoint>();
        private readonly IPublisher _inputQueue;
        private readonly string _streamName;
        private int _fromSequenceNumber;
        private readonly bool _resolveLinkTos;
        private readonly string _category;

        private bool _paused = true;
        private bool _pauseRequested = true;
        private bool _eventsRequested;
        private int _maxReadCount = 111;
        private bool _disposed;

        public StreamReaderEventDistributionPoint(
            IPublisher publisher, IPublisher inputQueue, Guid distibutionPointCorrelationId, string streamName, int fromSequenceNumber,
            bool resolveLinkTos, string category = null)
            : base(publisher, distibutionPointCorrelationId)
        {
            if (inputQueue == null) throw new ArgumentNullException("inputQueue");
            if (fromSequenceNumber < 0) throw new ArgumentException("fromSequenceNumber");
            if (streamName == null) throw new ArgumentNullException("streamName");
            if (string.IsNullOrEmpty(streamName)) throw new ArgumentException("streamName");
            _inputQueue = inputQueue;
            _streamName = streamName;
            _fromSequenceNumber = fromSequenceNumber;
            _resolveLinkTos = resolveLinkTos;
            _category = category;
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
            RequestEvents(delay: false);
        }

        public override void Pause()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_pauseRequested)
                throw new InvalidOperationException("Pause has been already requested");
            _pauseRequested = true;
            if (!_eventsRequested)
                _paused = true;
        }

        public override void Handle(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            if (_disposed)
                return;
            if (!_eventsRequested)
                throw new InvalidOperationException("Read events has not been requested");
            if (message.EventStreamId != _streamName)
                throw new InvalidOperationException(
                    string.Format("Invalid stream name: {0}.  Expected: {1}", message.EventStreamId, _streamName));
            if (_paused)
                throw new InvalidOperationException("Paused");
            _eventsRequested = false;
            switch (message.Result)
            {
                case RangeReadResult.NoStream:
                    DeliverLastCommitPosition(message.LastCommitPosition.Value); // allow joining heading distribution
                    RequestEvents(delay: true);

                    break;
                case RangeReadResult.Success:
                    if (message.Events.Length == 0)
                    {
                        // the end
                        DeliverLastCommitPosition(message.LastCommitPosition.Value);
                    }
                    else
                    {
                        for (int index = 0; index < message.Events.Length; index++)
                        {
                            var @event = message.Events[index];
                            var @link = message.LinkToEvents != null ? message.LinkToEvents[index] : null;
                            DeliverEvent(@event, @link);
                        }
                    }
                    if (_pauseRequested)
                        _paused = true;
                    else
                        if (message.Events.Length == 0)
                            RequestEvents(delay: true);
                        else
                            _publisher.Publish(CreateTickMessage());
                    break;
                default:
                    throw new NotSupportedException(
                        string.Format("ReadEvents result code was not recognized. Code: {0}", message.Result));
            }
        }

        public override void Handle(ClientMessage.ReadAllEventsForwardCompleted message)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            _disposed = true;
        }

        private void RequestEvents(bool delay)
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (_eventsRequested)
                throw new InvalidOperationException("Read operation is already in progress");
            if (_pauseRequested || _paused)
                throw new InvalidOperationException("Paused or pause requested");
            _eventsRequested = true;


            var readEventsForward = new ClientMessage.ReadStreamEventsForward(
                _distibutionPointCorrelationId, new SendToThisEnvelope(this), _streamName, _fromSequenceNumber,
                _maxReadCount, _resolveLinkTos);
            if (delay)
                _publisher.Publish(
                    TimerMessage.Schedule.Create(
                        TimeSpan.FromMilliseconds(250), new PublishEnvelope(_publisher, crossThread: true), readEventsForward));
            else
                _publisher.Publish(readEventsForward);
        }

        private ProjectionMessage.CoreService.Tick CreateTickMessage()
        {
            return new ProjectionMessage.CoreService.Tick(() => { if (!_paused && !_disposed) RequestEvents(delay: false); });
        }

        private void DeliverLastCommitPosition(long lastCommitPosition)
        {
            _publisher.Publish(
                new ProjectionMessage.Projections.CommittedEventReceived(
                    _distibutionPointCorrelationId, new EventPosition(long.MinValue, lastCommitPosition), _streamName,
                    _fromSequenceNumber, _streamName, _fromSequenceNumber, false, null));
        }

        private void DeliverEvent(EventRecord @event, EventRecord link)
        {
            EventRecord positionEvent = (link ?? @event);
            if (positionEvent.EventNumber != _fromSequenceNumber)
                throw new InvalidOperationException(
                    string.Format(
                        "Event number {0} was expected in the stream {1}, but event number {2} was received",
                        _fromSequenceNumber, _streamName, positionEvent.EventNumber));
            _fromSequenceNumber = positionEvent.EventNumber + 1;
            var resolvedLinkTo = positionEvent.EventStreamId != @event.EventStreamId
                                 || positionEvent.EventNumber != @event.EventNumber;
            _publisher.Publish(
                //TODO: publish bothlink and event data
                new ProjectionMessage.Projections.CommittedEventReceived(
                    _distibutionPointCorrelationId, new EventPosition(long.MinValue, positionEvent.LogPosition),
                    positionEvent.EventStreamId, positionEvent.EventNumber, @event.EventStreamId, @event.EventNumber,
                    resolvedLinkTo, new Event(@event.EventId, @event.EventType, false, @event.Data, @event.Metadata)));
        }
    }
}
