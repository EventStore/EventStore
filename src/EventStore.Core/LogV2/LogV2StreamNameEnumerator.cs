using System;
using System.Collections.Generic;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using StreamId = System.String;

namespace EventStore.Core.LogV2 {
	/// <summary>
	/// Stream name enumerator for Log V2
	/// Reads the transaction log forward and returns stream names from Prepare log records
	/// May return a stream name more than once.
	/// </summary>
	public class LogV2StreamNameEnumerator : INameEnumerator {
		private Func<TFReaderLease> _tfReaderFactory;
		private IReadOnlyCheckpoint _chaserCheckpoint;

		public LogV2StreamNameEnumerator(Func<TFReaderLease> tfReaderFactory, IReadOnlyCheckpoint chaserCheckpoint) {
			_tfReaderFactory = tfReaderFactory;
			_chaserCheckpoint = chaserCheckpoint;
		}

		public IEnumerable<(string name, long checkpoint)> EnumerateNames(long lastCheckpoint) {
			using var reader = _tfReaderFactory();
			reader.Reposition(Math.Max(0L, lastCheckpoint));

			var buildToPosition = _chaserCheckpoint.Read();
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
	}
}
