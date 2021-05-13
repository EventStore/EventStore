using System;
using System.Collections.Generic;
using System.Security.Claims;
using EventStore.ClientAPI.Common;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Util;

namespace EventStore.Core.Tests.TransactionLog {
	internal class FakeReadIndex<TLogFormat, TStreamId> : IReadIndex<TStreamId> {
		private readonly ISystemStreamLookup<TStreamId> _systemStreams =
			LogFormatHelper<TLogFormat, TStreamId>.LogFormat.SystemStreams;

		public long LastIndexedPosition {
			get { throw new NotImplementedException(); }
		}
		
		public IIndexWriter<TStreamId> IndexWriter {
			get { throw new NotImplementedException(); }
		}

		private readonly Func<TStreamId, bool> _isStreamDeleted;

		public FakeReadIndex(Func<TStreamId, bool> isStreamDeleted) {
			Ensure.NotNull(isStreamDeleted, "isStreamDeleted");
			_isStreamDeleted = isStreamDeleted;
		}

		public void Init(long buildToPosition) {
			throw new NotImplementedException();
		}

		public void Commit(CommitLogRecord record) {
			throw new NotImplementedException();
		}

		public void Commit(IList<IPrepareLogRecord<TStreamId>> commitedPrepares) {
			throw new NotImplementedException();
		}

		public ReadIndexStats GetStatistics() {
			throw new NotImplementedException();
		}

		public IndexReadEventResult ReadEvent(string streamName, TStreamId streamId, long eventNumber) {
			throw new NotImplementedException();
		}

		public IndexReadStreamResult ReadStreamEventsBackward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount) {
			throw new NotImplementedException();
		}

		public IndexReadStreamResult ReadStreamEventsForward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount) {
			throw new NotImplementedException();
		}

		public IndexReadAllResult ReadAllEventsForward(TFPos pos, int maxCount) {
			throw new NotImplementedException();
		}

		public IndexReadAllResult ReadAllEventsBackward(TFPos pos, int maxCount) {
			throw new NotImplementedException();
		}

		public IndexReadAllResult ReadAllEventsForwardFiltered(TFPos pos, int maxCount, int maxSearchWindow,
			IEventFilter eventFilter) {
			throw new NotImplementedException();
		}

		public IndexReadAllResult ReadAllEventsBackwardFiltered(TFPos pos, int maxCount, int maxSearchWindow,
			IEventFilter eventFilter) {
			throw new NotImplementedException();
		}

		public bool IsStreamDeleted(TStreamId streamId) {
			return _isStreamDeleted(streamId);
		}

		public long GetStreamLastEventNumber(TStreamId streamId) {
			if (_systemStreams.IsMetaStream(streamId))
				return GetStreamLastEventNumber(_systemStreams.OriginalStreamOf(streamId));
			return _isStreamDeleted(streamId) ? EventNumber.DeletedStream : 1000000;
		}

		public StorageMessage.EffectiveAcl GetEffectiveAcl(TStreamId streamId) {
			throw new NotImplementedException();
		}

		public TStreamId GetEventStreamIdByTransactionId(long transactionId) {
			throw new NotImplementedException();
		}

		public StreamAccess CheckStreamAccess(TStreamId streamId, StreamAccessType streamAccessType, ClaimsPrincipal user) {
			throw new NotImplementedException();
		}

		public StreamMetadata GetStreamMetadata(TStreamId streamId) {
			throw new NotImplementedException();
		}

		public TStreamId GetStreamId(string streamName) {
			throw new NotImplementedException();
		}

		public string GetStreamName(TStreamId streamId) {
			throw new NotImplementedException();
		}

		public void Close() {
			throw new NotImplementedException();
		}

		public void Dispose() {
			throw new NotImplementedException();
		}
	}
}
