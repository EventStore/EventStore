using System;
using System.Collections.Generic;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using StreamId = System.String;

namespace EventStore.Core.LogV2 {
	/// <summary>
	/// Stream name existence filter initializer for Log V2
	/// Reads the index and transaction log to populate the stream name existence filter from the last checkpoint.
	/// May add a stream hash more than once.
	/// </summary>
	public class LogV2StreamNameExistenceFilterInitializer : INameExistenceFilterInitializer {
		private readonly Func<TFReaderLease> _tfReaderFactory;
		private readonly IReadOnlyCheckpoint _chaserCheckpoint;
		private readonly IHasher<string> _lowHasher;
		private readonly IHasher<string> _highHasher;
		private ITableIndex _tableIndex;

		public LogV2StreamNameExistenceFilterInitializer(
			Func<TFReaderLease> tfReaderFactory,
			IReadOnlyCheckpoint chaserCheckpoint,
			IHasher<string> lowHasher,
			IHasher<string> highHasher) {
			_tfReaderFactory = tfReaderFactory;
			_chaserCheckpoint = chaserCheckpoint;
			_lowHasher = lowHasher;
			_highHasher = highHasher;
		}

		public void SetTableIndex(ITableIndex tableIndex) {
			_tableIndex = tableIndex;
		}

		private ulong Hash(string streamId) {
			//qq consider how we want to deal with high and low, they're back to front here but also in the normal index
			return (ulong)_lowHasher.Hash(streamId) << 32 | _highHasher.Hash(streamId);
		}

		private IEnumerable<(ulong streamHash, long checkpoint)> EnumerateStreamHashes(long lastCheckpoint) {
			if (_tableIndex == null) throw new Exception("Call SetTableIndex first");

			using var reader = _tfReaderFactory();
			var buildToPosition = _chaserCheckpoint.Read();

			if (lastCheckpoint == -1L) { // if we do not have a checkpoint, rebuild the list of stream hashes from the index
				ulong previousHash = ulong.MaxValue;
				foreach (var entry in _tableIndex.IterateAll()) {
					if (entry.Stream == previousHash) {
						continue;
					}
					previousHash = entry.Stream;
					yield return (previousHash, -1L);
				}
				if (previousHash != ulong.MaxValue) { // send a checkpoint with the last stream hash
					yield return (previousHash, Math.Max(_tableIndex.PrepareCheckpoint, _tableIndex.CommitCheckpoint));
				}
				reader.Reposition(Math.Max(_tableIndex.PrepareCheckpoint, _tableIndex.CommitCheckpoint));
			} else {
				reader.Reposition(lastCheckpoint);
			}

			while (true) {
				if (!TryReadNextLogRecord(reader, buildToPosition, out var record, out var postPosition)) {
					break;
				}
				switch (record.RecordType) {
					case LogRecordType.Prepare:
						var prepare = (IPrepareLogRecord<StreamId>) record;
						if (prepare.Flags.HasFlag(PrepareFlags.IsCommitted)) {
							yield return (Hash(prepare.EventStreamId), postPosition);
						}
						break;
					case LogRecordType.Commit:
						var commit = (CommitLogRecord)record;
						reader.Reposition(commit.TransactionPosition);
						if (TryReadNextLogRecord(reader, buildToPosition, out var transactionRecord, out _)) {
							var transactionPrepare = (IPrepareLogRecord<StreamId>) transactionRecord;
							yield return (Hash(transactionPrepare.EventStreamId), postPosition);
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

		public void Initialize(INameExistenceFilter filter) {
			var lastCheckpoint = filter.CurrentCheckpoint;
			foreach (var (hash, checkpoint) in EnumerateStreamHashes(lastCheckpoint)) {
				filter.Add(hash, checkpoint);
			}
		}
	}
}
