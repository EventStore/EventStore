using System;
using System.Collections.Generic;
using EventStore.Core.Index;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using StreamId = System.String;

namespace EventStore.Core.LogV2 {
	/// <summary>
	/// Stream existence filter initializer for Log V2
	/// Reads the index and transaction log to populate the stream existence filter from the last checkpoint.
	/// May add a stream hash more than once.
	/// </summary>
	/// In V2 the the bloom filter checkpoint is the commit position of the last processed
	/// log record.
	public class LogV2StreamExistenceFilterInitializer : INameExistenceFilterInitializer {
		private readonly Func<TFReaderLease> _tfReaderFactory;
		private readonly IReadOnlyCheckpoint _chaserCheckpoint;
		private readonly ITableIndex _tableIndex;

		public LogV2StreamExistenceFilterInitializer(
			Func<TFReaderLease> tfReaderFactory,
			IReadOnlyCheckpoint chaserCheckpoint,
			ITableIndex tableIndex) {
			_tfReaderFactory = tfReaderFactory;
			_chaserCheckpoint = chaserCheckpoint;
			_tableIndex = tableIndex;
		}

		//qq hopefully we can get rid of this by having the index iterate through the inmemory parts.
		private IEnumerable<(string streamName, long checkpoint)> EnumerateStreamsInLog(long lastCheckpoint) {
			using var reader = _tfReaderFactory();
			var buildToPosition = _chaserCheckpoint.Read();

			reader.Reposition(lastCheckpoint);
			while (true) {
				if (!TryReadNextLogRecord(reader, buildToPosition, out var record, out var postPosition)) {
					break;
				}
				switch (record.RecordType) {
					case LogRecordType.Prepare:
						var prepare = (IPrepareLogRecord<StreamId>) record;
						if (prepare.Flags.HasFlag(PrepareFlags.IsCommitted)) {
							yield return (prepare.EventStreamId, postPosition);
						}
						break;
					case LogRecordType.Commit:
						var commit = (CommitLogRecord)record;
						reader.Reposition(commit.TransactionPosition);
						if (TryReadNextLogRecord(reader, buildToPosition, out var transactionRecord, out _)) {
							var transactionPrepare = (IPrepareLogRecord<StreamId>) transactionRecord;
							yield return (transactionPrepare.EventStreamId, postPosition);
						} else {
							// nothing to do - may have been scavenged
						}
						reader.Reposition(postPosition);
						break;
				}

			}
		}

		private IEnumerable<(ulong streamHash, long checkpoint)> EnumerateStreamsInIndex() {
			ulong previousHash = ulong.MaxValue;
			foreach (var entry in _tableIndex.IterateAll()) {
				if (entry.Stream == previousHash) {
					continue;
				}
				yield return (entry.Stream, -1L);
				previousHash = entry.Stream;
			}
			if (previousHash != ulong.MaxValue) { // send a checkpoint with the last stream hash
				yield return (previousHash, Math.Max(_tableIndex.PrepareCheckpoint, _tableIndex.CommitCheckpoint));
			}
		}

		public static bool TryReadNextLogRecord(TFReaderLease reader, long maxPosition, out ILogRecord record, out long postPosition) {
			var result = reader.TryReadNext();
			if (!result.Success || result.LogRecord.LogPosition >= maxPosition) {
				record = null;
				postPosition = 0L;
				return false;
			}
			record = result.LogRecord;
			postPosition = result.RecordPostPosition;
			return true;
		}

		public void Initialize(INameExistenceFilter filter) {
			if (_tableIndex == null) throw new Exception("Call SetTableIndex first");

			var lastCheckpoint = filter.CurrentCheckpoint;
			if (lastCheckpoint == -1L) { //if we do not have a checkpoint, populate the filter from the index first
				foreach (var (hash, checkpoint) in EnumerateStreamsInIndex()) {
					filter.Add(hash, checkpoint);
					lastCheckpoint = Math.Max(lastCheckpoint, checkpoint);
				}
				lastCheckpoint = Math.Max(lastCheckpoint, 0L); //empty table index
			}

			//then populate the filter with remaining stream names from the log
			foreach (var (name, checkpoint) in EnumerateStreamsInLog(lastCheckpoint)) {
				filter.Add(name, checkpoint);
			}
		}
	}
}
