using System;
using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ExternallyFedByStreamEventReader : EventReader,
        IHandle<ReaderSubscriptionManagement.SpoolStreamReadingCore>,
        IHandle<ReaderSubscriptionManagement.CompleteSpooledStreamReading>
    {
        private readonly IODispatcher _ioDispatcher;
        private long? _limitingCommitPosition;
        private readonly ITimeProvider _timeProvider;
        private readonly bool _resolveLinkTos;

        private int _maxReadCount = 111;
        private int _deliveredEvents;

        private string _dataStreamName;
        private int _dataNextSequenceNumber;
        private readonly Queue<Tuple<string, int>> _pendingStreams = new Queue<Tuple<string, int>>();

        private Guid _dataReadRequestId;
        private bool _catalogEof;
        private int _catalogCurrentSequenceNumber;
        private readonly HashSet<Guid> _readLengthRequests = new HashSet<Guid>();


        public ExternallyFedByStreamEventReader(
            IPublisher publisher, Guid eventReaderCorrelationId, IPrincipal readAs, IODispatcher ioDispatcher,
            long? limitingCommitPosition, ITimeProvider timeProvider, bool resolveLinkTos)
            : base(ioDispatcher, publisher, eventReaderCorrelationId, readAs, true, stopAfterNEvents: null)
        {
            _ioDispatcher = ioDispatcher;
            _limitingCommitPosition = limitingCommitPosition;
            _timeProvider = timeProvider;
            _resolveLinkTos = resolveLinkTos;
            _catalogCurrentSequenceNumber = -1;
        }

        protected override bool AreEventsRequested()
        {
            return _dataReadRequestId != Guid.Empty;
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

        protected override void RequestEvents()
        {
            if (_disposed) throw new InvalidOperationException("Disposed");

            if (AreEventsRequested())
                throw new InvalidOperationException("Read operation is already in progress");
            if (PauseRequested || Paused)
                throw new InvalidOperationException("Paused or pause requested");

            TakeNextStreamIfRequired();
            if (!_disposed && _dataStreamName != null)
            {
                _dataReadRequestId = _ioDispatcher.ReadForward(
                    _dataStreamName, _dataNextSequenceNumber, _maxReadCount, _resolveLinkTos, ReadAs,
                    ReadDataStreamCompleted);
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
                            0, "", _catalogCurrentSequenceNumber, _dataStreamName, int.MaxValue,
                            _limitingCommitPosition.Value));
                _dataStreamName = null;
                if (_catalogEof && _pendingStreams.Count == 0)
                {
                    SendEof();
                    return;
                }
                if (_pendingStreams.Count == 0)
                {
                    SendIdle();
                    return;
                }
                var nextDataStream = _pendingStreams.Dequeue();
                _dataStreamName = nextDataStream.Item1;
                _catalogCurrentSequenceNumber = nextDataStream.Item2;
                _dataNextSequenceNumber = 0;
            }
        }

        private void SendIdle()
        {
            _publisher.Publish(
                new ReaderSubscriptionMessage.EventReaderIdle(EventReaderCorrelationId, _timeProvider.Now));
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
                    _dataNextSequenceNumber = int.MaxValue;
                    if (completed.LastEventNumber >= 0)
                        SendPartitionDeleted_WhenReadingDataStream(_dataStreamName, -1, null, null, null, null);
                    PauseOrContinueProcessing();
                    break;
                case ReadStreamResult.StreamDeleted:
                    _dataNextSequenceNumber = int.MaxValue;
                    SendPartitionDeleted_WhenReadingDataStream(_dataStreamName, -1, null, null, null, null);
                    PauseOrContinueProcessing();
                    break;
                case ReadStreamResult.Success:
                    foreach (var e in completed.Events)
                    {
                        DeliverEvent(e, 17.7f);
                        if (CheckEnough())
                            return;
                    }
                    if (completed.IsEndOfStream)
                        _dataNextSequenceNumber = int.MaxValue;
                    else
                        _dataNextSequenceNumber = completed.NextEventNumber;
                    PauseOrContinueProcessing();
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private void EnqueueStreamForProcessing(string streamId, int catalogSequenceNumber)
        {
            _pendingStreams.Enqueue(Tuple.Create(streamId, catalogSequenceNumber));
            if (!AreEventsRequested() && !PauseRequested && !Paused)
                RequestEvents();
        }

        private void CompleteStreamProcessing()
        {
            _catalogEof = true;
        }

        private void DeliverEvent(EventStore.Core.Data.ResolvedEvent pair, float progress)
        {
            _deliveredEvents++;

            EventRecord positionEvent = pair.OriginalEvent;
            if (positionEvent.LogPosition > _limitingCommitPosition)
                return;

            var resolvedEvent = new ResolvedEvent(pair, null);
            if (resolvedEvent.IsLinkToDeletedStream || resolvedEvent.IsStreamDeletedEvent)
                return;
            _publisher.Publish(
                //TODO: publish both link and event data
                new ReaderSubscriptionMessage.CommittedEventDistributed(
                    EventReaderCorrelationId, resolvedEvent,
                    _stopOnEof ? (long?) null : positionEvent.LogPosition, progress, source: GetType(),
                    preTagged:
                        CheckpointTag.FromByStreamPosition(
                            0, "", _catalogCurrentSequenceNumber, positionEvent.EventStreamId, positionEvent.EventNumber,
                            _limitingCommitPosition.Value)));
            //TODO: consider passing phase from outside instead of using 0 (above)
        }

        public void Handle(ReaderSubscriptionManagement.SpoolStreamReadingCore message)
        {
            EnsureLimitingCommitPositionSet(message.LimitingCommitPosition);
            BeginReadStreamLength(message.StreamId);
            EnqueueStreamForProcessing(message.StreamId, message.CatalogSequenceNumber);
        }

        private void BeginReadStreamLength(string streamId)
        {
            var requestId = _ioDispatcher.ReadBackward(
                streamId, -1, 1, false, ReadAs, completed =>
                {
                    _readLengthRequests.Remove(_dataReadRequestId);
                    switch (completed.Result)
                    {
                        case ReadStreamResult.AccessDenied:
                            SendNotAuthorized();
                            break;
                        case ReadStreamResult.NoStream:
                            DeliverStreamLength(streamId, 0);
                            break;
                        case ReadStreamResult.StreamDeleted:
                            DeliverStreamLength(streamId, 0);
                            break;
                        case ReadStreamResult.Success:
                            DeliverStreamLength(streamId, completed.LastEventNumber);
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                });
            if (requestId != Guid.Empty)
                _readLengthRequests.Add(requestId);
        }

        private void DeliverStreamLength(string streamId, int length)
        {
            _publisher.Publish(
                //TODO: publish both link and event data
                new ReaderSubscriptionMessage.EventReaderPartitionMeasured(EventReaderCorrelationId, streamId, length));
        }

        private void EnsureLimitingCommitPositionSet(long limitingCommitPosition)
        {
            if (_limitingCommitPosition != null && _limitingCommitPosition.GetValueOrDefault() != limitingCommitPosition)
                throw new InvalidOperationException(
                    string.Format(
                        "ExternallyFedByStreamEventReader cannot be used with different limiting commit positions.  "
                        + "Currently set: {0}. New: {1}", _limitingCommitPosition, limitingCommitPosition));
            _limitingCommitPosition = limitingCommitPosition;
        }

        public void Handle(ReaderSubscriptionManagement.CompleteSpooledStreamReading message)
        {
            CompleteStreamProcessing();
        }

    }
}
