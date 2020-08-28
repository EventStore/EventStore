﻿using System;
using System.Collections.Generic;
using System.Security.Claims;
using EventStore.ClientAPI.Common;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Util;

namespace EventStore.Core.Tests.TransactionLog {
	internal class FakeReadIndex : IReadIndex {
		public long LastIndexedPosition {
			get { throw new NotImplementedException(); }
		}
		
		public IIndexWriter IndexWriter {
			get { throw new NotImplementedException(); }
		}

		private readonly Func<string, bool> _isStreamDeleted;

		public FakeReadIndex(Func<string, bool> isStreamDeleted) {
			Ensure.NotNull(isStreamDeleted, "isStreamDeleted");
			_isStreamDeleted = isStreamDeleted;
		}

		public void Init(long buildToPosition) {
			throw new NotImplementedException();
		}

		public void Commit(CommitLogRecord record) {
			throw new NotImplementedException();
		}

		public void Commit(IList<PrepareLogRecord> commitedPrepares) {
			throw new NotImplementedException();
		}

		public ReadIndexStats GetStatistics() {
			throw new NotImplementedException();
		}

		public IndexReadEventResult ReadEvent(string streamId, long eventNumber) {
			throw new NotImplementedException();
		}

		public IndexReadStreamResult ReadStreamEventsBackward(string streamId, long fromEventNumber, int maxCount) {
			throw new NotImplementedException();
		}

		public IndexReadStreamResult ReadStreamEventsForward(string streamId, long fromEventNumber, int maxCount) {
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

		public bool IsStreamDeleted(string streamId) {
			return _isStreamDeleted(streamId);
		}

		public long GetStreamLastEventNumber(string streamId) {
			if (SystemStreams.IsMetastream(streamId))
				return GetStreamLastEventNumber(SystemStreams.OriginalStreamOf(streamId));
			return _isStreamDeleted(streamId) ? EventNumber.DeletedStream : 1000000;
		}

		public StorageMessage.EffectiveAcl GetEffectiveAcl(string streamId) {
			throw new NotImplementedException();
		}

		public string GetEventStreamIdByTransactionId(long transactionId) {
			throw new NotImplementedException();
		}

		public StreamAccess CheckStreamAccess(string streamId, StreamAccessType streamAccessType, ClaimsPrincipal user) {
			throw new NotImplementedException();
		}

		public StreamMetadata GetStreamMetadata(string streamId) {
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
