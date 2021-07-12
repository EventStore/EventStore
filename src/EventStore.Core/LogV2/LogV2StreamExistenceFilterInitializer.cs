using System;
using EventStore.Common.Utils;
using EventStore.Core.Index;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;

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
		private readonly ITableIndex _tableIndex;

		public LogV2StreamExistenceFilterInitializer(
			Func<TFReaderLease> tfReaderFactory,
			ITableIndex tableIndex) {

			Ensure.NotNull(tableIndex, nameof(tableIndex));

			_tfReaderFactory = tfReaderFactory;
			_tableIndex = tableIndex;
		}

		public void Initialize(INameExistenceFilter filter) {
			InitializeFromIndex(filter);
			InitializeFromLog(filter);
		}

		private void InitializeFromIndex(INameExistenceFilter filter) {
			if (filter.CurrentCheckpoint != -1L) {
				// can only use the index to build from scratch. if we have a checkpoint
				// we need to build from the log in order to make use of it.
				return;
			}

			// we have no checkpoint, build from the index. unfortunately there isn't
			// a simple way to checkpoint in the middle of the index.
			ulong? previousHash = null;
			foreach (var entry in _tableIndex.IterateAllInOrder()) {
				if (entry.Stream == previousHash)
					continue;

				// add regardless of version because event 0 may be scavenged
				filter.Add(entry.Stream, -1);
				previousHash = entry.Stream;
			}

			// checkpoint at the end of the index.
			if (previousHash != null) {
				var checkpoint = _tableIndex.CommitCheckpoint;
				filter.Add(previousHash.Value, checkpoint);
			}
		}

		private void InitializeFromLog(INameExistenceFilter filter) {
			using var reader = _tfReaderFactory();

			// if we have a checkpoint, start from that position in the log. this will work
			// whether the checkpoint is the pre or post position of the last processed record.
			var startPosition = filter.CurrentCheckpoint == -1 ? 0 : filter.CurrentCheckpoint;
			reader.Reposition(startPosition);

			while (TryReadNextLogRecord(reader, out var record, out var postPosition)) {
				switch (record.RecordType) {
					case LogRecordType.Prepare:
						var prepare = (IPrepareLogRecord<string>)record;
						if (prepare.Flags.HasFlag(PrepareFlags.IsCommitted)) {
							filter.Add(prepare.EventStreamId, postPosition);
						}
						break;
					case LogRecordType.Commit:
						var commit = (CommitLogRecord)record;
						reader.Reposition(commit.TransactionPosition);
						if (TryReadNextLogRecord(reader, out var transactionRecord, out _)) {
							var transactionPrepare = (IPrepareLogRecord<string>)transactionRecord;
							filter.Add(transactionPrepare.EventStreamId, postPosition);
						} else {
							// nothing to do - may have been scavenged
							//qqq yehbut prepares later in this transaction may not have been scavenged, would have to scan
							// the log until the commit position like the allreader does.
							// may be better to just add the prepares even when not committed.
						}
						reader.Reposition(postPosition);
						break;
				}
			}
		}

		private static bool TryReadNextLogRecord(TFReaderLease reader, out ILogRecord record, out long postPosition) {
			var result = reader.TryReadNext();
			if (!result.Success) {
				record = default;
				postPosition = default;
				return false;
			}
			record = result.LogRecord;
			postPosition = result.RecordPostPosition;
			return true;
		}
	}
}
