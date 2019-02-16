using System;
using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class ByStreamCatalogEventReader : EventReader {
		private readonly IODispatcher _ioDispatcher;
		private readonly string _catalogStreamName;
		private long _catalogCurrentSequenceNumber;
		private long _catalogNextSequenceNumber;
		private long? _limitingCommitPosition;
		private readonly bool _resolveLinkTos;

		private int _maxReadCount = 111;

		private string _dataStreamName;
		private long _dataNextSequenceNumber;
		private readonly Queue<string> _pendingStreams = new Queue<string>();

		private Guid _catalogReadRequestId;
		private Guid _dataReadRequestId;
		private bool _catalogEof;


		public ByStreamCatalogEventReader(
			IPublisher publisher,
			Guid eventReaderCorrelationId,
			IPrincipal readAs,
			IODispatcher ioDispatcher,
			string catalogCatalogStreamName,
			long catalogNextSequenceNumber,
			string dataStreamName,
			long dataNextSequenceNumber,
			long? limitingCommitPosition,
			bool resolveLinkTos)
			: base(publisher, eventReaderCorrelationId, readAs, true) {
			_ioDispatcher = ioDispatcher;
			_catalogStreamName = catalogCatalogStreamName;
			_catalogCurrentSequenceNumber = catalogNextSequenceNumber - 1;
			_catalogNextSequenceNumber = catalogNextSequenceNumber;
			_dataStreamName = dataStreamName;
			_dataNextSequenceNumber = dataNextSequenceNumber;
			_limitingCommitPosition = limitingCommitPosition;
			_resolveLinkTos = resolveLinkTos;
		}

		protected override bool AreEventsRequested() {
			return _catalogReadRequestId != Guid.Empty || _dataReadRequestId != Guid.Empty;
		}

		protected override void RequestEvents() {
			if (_disposed) throw new InvalidOperationException("Disposed");

			if (AreEventsRequested())
				throw new InvalidOperationException("Read operation is already in progress");
			if (PauseRequested || Paused)
				throw new InvalidOperationException("Paused or pause requested");

			if (_pendingStreams.Count < 10 && !_catalogEof) {
				_catalogReadRequestId = _ioDispatcher.ReadForward(
					_catalogStreamName, _catalogNextSequenceNumber, _maxReadCount, false, ReadAs, ReadCatalogCompleted);
			} else {
				TakeNextStreamIfRequired();
				if (!_disposed) {
					_dataReadRequestId = _ioDispatcher.ReadForward(
						_dataStreamName, _dataNextSequenceNumber, _maxReadCount, _resolveLinkTos, ReadAs,
						ReadDataStreamCompleted);
				}
			}
		}

		private void TakeNextStreamIfRequired() {
			if (_dataNextSequenceNumber == EventNumber.DeletedStream || _dataStreamName == null) {
				if (_dataStreamName != null)
					SendPartitionEof(
						_dataStreamName,
						CheckpointTag.FromByStreamPosition(
							0, _catalogStreamName, _catalogCurrentSequenceNumber, _dataStreamName,
							EventNumber.DeletedStream,
							_limitingCommitPosition.Value));

				if (_catalogEof && _pendingStreams.Count == 0) {
					SendEof();
					return;
				}

				_dataStreamName = _pendingStreams.Dequeue();
				_catalogCurrentSequenceNumber++;
				_dataNextSequenceNumber = 0;
			}
		}

		private void ReadDataStreamCompleted(ClientMessage.ReadStreamEventsForwardCompleted completed) {
			_dataReadRequestId = Guid.Empty;

			if (Paused)
				throw new InvalidOperationException("Paused");

			switch (completed.Result) {
				case ReadStreamResult.AccessDenied:
					SendNotAuthorized();
					return;
				case ReadStreamResult.NoStream:
					_dataNextSequenceNumber = EventNumber.DeletedStream;
					if (completed.LastEventNumber >= 0)
						SendPartitionDeleted_WhenReadingDataStream(_dataStreamName, -1, null, null, null, null);
					PauseOrContinueProcessing();
					break;
				case ReadStreamResult.StreamDeleted:
					_dataNextSequenceNumber = EventNumber.DeletedStream;
					SendPartitionDeleted_WhenReadingDataStream(_dataStreamName, -1, null, null, null, null);
					PauseOrContinueProcessing();
					break;
				case ReadStreamResult.Success:
					foreach (var e in completed.Events)
						DeliverEvent(e, 17.7f);
					if (completed.IsEndOfStream)
						_dataNextSequenceNumber = EventNumber.DeletedStream;
					PauseOrContinueProcessing();
					break;
				default:
					throw new NotSupportedException();
			}
		}

		private void ReadCatalogCompleted(ClientMessage.ReadStreamEventsForwardCompleted completed) {
			_catalogReadRequestId = Guid.Empty;

			if (Paused)
				throw new InvalidOperationException("Paused");

			switch (completed.Result) {
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
					PauseOrContinueProcessing();
					break;
				default:
					throw new NotSupportedException();
			}
		}

		private void EnqueueStreamForProcessing(EventStore.Core.Data.ResolvedEvent resolvedEvent) {
			//TODO: consider catalog referring to earlier written events (should we check here?)
			if (resolvedEvent.OriginalEvent.LogPosition > _limitingCommitPosition)
				return;
			var streamId =
				SystemEventTypes.StreamReferenceEventToStreamId(resolvedEvent.Event.EventType,
					resolvedEvent.Event.Data);
			_pendingStreams.Enqueue(streamId);
			_catalogNextSequenceNumber = resolvedEvent.OriginalEventNumber;
		}

		private void DeliverEvent(EventStore.Core.Data.ResolvedEvent pair, float progress) {
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
					_stopOnEof ? (long?)null : positionEvent.LogPosition, progress, source: GetType(),
					preTagged:
					CheckpointTag.FromByStreamPosition(
						0, _catalogStreamName, _catalogCurrentSequenceNumber, positionEvent.EventStreamId,
						positionEvent.EventNumber, _limitingCommitPosition.Value)));
			//TODO: consider passing phase from outside instead of using 0 (above)
		}
	}
}
