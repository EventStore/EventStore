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
using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ByStreamCatalogEventReader : EventReader
    {
        private readonly IODispatcher _ioDispatcher;
        private readonly string _catalogStreamName;
        private int _catalogCurrentSequenceNumber;
        private int _catalogNextSequenceNumber;
        private long? _limitingCommitPosition;
        private readonly ITimeProvider _timeProvider;
        private readonly bool _resolveLinkTos;

        private int _maxReadCount = 111;
        private int _deliveredEvents;

        private string _dataStreamName;
        private int _dataNextSequenceNumber;
        private readonly Queue<string> _pendingStreams = new Queue<string>();

        private Guid _catalogReadRequestId;
        private Guid _dataReadRequestId;
        private bool _catalogEof;


        public ByStreamCatalogEventReader(
            IPublisher publisher, Guid eventReaderCorrelationId, IPrincipal readAs, IODispatcher ioDispatcher,
            string catalogCatalogStreamName, int catalogNextSequenceNumber, string dataStreamName,
            int dataNextSequenceNumber, long? limitingCommitPosition, ITimeProvider timeProvider, bool resolveLinkTos,
            int? stopAfterNEvents = null)
            : base(publisher, eventReaderCorrelationId, readAs, true, stopAfterNEvents)
        {

            _ioDispatcher = ioDispatcher;
            _catalogStreamName = catalogCatalogStreamName;
            _catalogCurrentSequenceNumber = catalogNextSequenceNumber - 1;
            _catalogNextSequenceNumber = catalogNextSequenceNumber;
            _dataStreamName = dataStreamName;
            _dataNextSequenceNumber = dataNextSequenceNumber;
            _limitingCommitPosition = limitingCommitPosition;
            _timeProvider = timeProvider;
            _resolveLinkTos = resolveLinkTos;
        }

        protected override bool AreEventsRequested()
        {
            return _catalogReadRequestId != Guid.Empty || _dataReadRequestId != Guid.Empty;
        }

        private bool CheckEnough()
        {
            if (_stopAfterNEvents != null && _deliveredEvents >= _stopAfterNEvents)
            {
                _publisher.Publish(
                    new ReaderSubscriptionMessage.EventReaderEof(EventReaderCorrelationId, maxEventsReached: true));
                Dispose();
                return true;
            }
            return false;
        }

        protected override void RequestEvents(bool delay)
        {
            if (_disposed) throw new InvalidOperationException("Disposed");
            if (delay)
                throw new NotSupportedException();

            if (AreEventsRequested())
                throw new InvalidOperationException("Read operation is already in progress");
            if (PauseRequested || Paused)
                throw new InvalidOperationException("Paused or pause requested");

            if (_pendingStreams.Count < 10 && !_catalogEof)
            {
                _catalogReadRequestId = _ioDispatcher.ReadForward(
                    _catalogStreamName, _catalogNextSequenceNumber, _maxReadCount, false, ReadAs, ReadCatalogCompleted);
            }
            else
            {
                TakeNextStreamIfRequired();
                if (!_disposed)
                {
                    _dataReadRequestId = _ioDispatcher.ReadForward(
                        _dataStreamName, _dataNextSequenceNumber, _maxReadCount, _resolveLinkTos, ReadAs,
                        ReadDataStreamCompleted);
                }
            }

        }

        private void TakeNextStreamIfRequired()
        {
            if (_dataNextSequenceNumber == int.MaxValue || _dataStreamName == null)
            {
                if (_dataStreamName != null)
                    SendPartitionEof(
                        _dataStreamName,
                        CheckpointTag.FromByStreamPosition(
                            0, _catalogStreamName, _catalogCurrentSequenceNumber, _dataStreamName, int.MaxValue,
                            _limitingCommitPosition.Value));
                
                if (_catalogEof && _pendingStreams.Count == 0)
                {
                    SendEof();
                    return;
                }
                _dataStreamName = _pendingStreams.Dequeue();
                _catalogCurrentSequenceNumber++;
                _dataNextSequenceNumber = 0;

            }
        }

        private void ReadDataStreamCompleted(ClientMessage.ReadStreamEventsForwardCompleted completed)
        {
            _dataReadRequestId = Guid.Empty;

            if (Paused)
                throw new InvalidOperationException("Paused");

            switch (completed.Result)
            {
                case ReadStreamResult.AccessDenied:
                    SendNotAuthorized();
                    return;
                case ReadStreamResult.NoStream:
                case ReadStreamResult.StreamDeleted:
                    _dataNextSequenceNumber = int.MaxValue;
                    PauseOrContinueProcessing(delay: false);
                    break;
                case ReadStreamResult.Success:
                    foreach (var e in completed.Events)
                    {
                        DeliverEvent(e.Event, e.Link, 17.7f);
                        if (CheckEnough())
                            return;
                    }
                    if (completed.IsEndOfStream)
                        _dataNextSequenceNumber = int.MaxValue;
                    PauseOrContinueProcessing(delay: false);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private void ReadCatalogCompleted(ClientMessage.ReadStreamEventsForwardCompleted completed)
        {
            _catalogReadRequestId = Guid.Empty;

            if (Paused)
                throw new InvalidOperationException("Paused");

            switch (completed.Result)
            {
                case ReadStreamResult.AccessDenied:
                    SendNotAuthorized();
                    return;
                case ReadStreamResult.NoStream:
                case ReadStreamResult.StreamDeleted:
                    _catalogEof = true;
                    SendEof();
                    return;
                case ReadStreamResult.Success:
                    _limitingCommitPosition = _limitingCommitPosition ?? completed.TfLastCommitPosition;
                    foreach (var e in completed.Events)
                        EnqueueStreamForProcessing(e);
                    if (completed.IsEndOfStream)
                        _catalogEof = true;
                    PauseOrContinueProcessing(delay: false);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private void EnqueueStreamForProcessing(EventStore.Core.Data.ResolvedEvent resolvedEvent)
        {
            //TODO: consider catalog referring to earlier written events (should we check here?)
            if (resolvedEvent.OriginalEvent.LogPosition > _limitingCommitPosition)
                return;
            var streamId = SystemEventTypes.StreamReferenceEventToStreamId(resolvedEvent.Event.EventType, resolvedEvent.Event.Data);
            _pendingStreams.Enqueue(streamId);
            _catalogNextSequenceNumber = resolvedEvent.OriginalEventNumber;
        }

        private void DeliverEvent(EventRecord @event, EventRecord link, float progress)
        {
            _deliveredEvents++;

            EventRecord positionEvent = (link ?? @event);
            if (positionEvent.LogPosition > _limitingCommitPosition)
                return;

            var resolvedLinkTo = positionEvent.EventStreamId != @event.EventStreamId
                                 || positionEvent.EventNumber != @event.EventNumber;
            _publisher.Publish(
                //TODO: publish both link and event data
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId,
                    new ResolvedEvent(
                        positionEvent.EventStreamId, positionEvent.EventNumber, @event.EventStreamId, @event.EventNumber,
                        resolvedLinkTo, new TFPos(-1, positionEvent.LogPosition), new TFPos(-1, @event.LogPosition),
                        @event.EventId, @event.EventType, (@event.Flags & PrepareFlags.IsJson) != 0, @event.Data,
                        @event.Metadata, link == null ? null : link.Metadata, positionEvent.TimeStamp),
                    _stopOnEof ? (long?) null : positionEvent.LogPosition, progress, source: GetType(),
                    preTagged:
                        CheckpointTag.FromByStreamPosition(
                            0, _catalogStreamName, _catalogCurrentSequenceNumber, positionEvent.EventStreamId,
                            positionEvent.EventNumber, _limitingCommitPosition.Value)));
            //TODO: consider passing phase from outside instead of using 0 (above)
        }
    }
}
