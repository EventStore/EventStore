using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ChunkReaderForIndexExecutor : IChunkReaderForIndexExecutor<string> {
		private readonly Func<TFReaderLease> _tfReaderFactory;

		public ChunkReaderForIndexExecutor(Func<TFReaderLease> tfReaderFactory) {
			_tfReaderFactory = tfReaderFactory;
		}

		public bool TryGetStreamId(long position, out string streamId) {
			using (var reader = _tfReaderFactory()) {
				var result = reader.TryReadAt(position);
				if (!result.Success) {
					streamId = default;
					return false;
				}

				if (!(result.LogRecord is PrepareLogRecord prepare))
					throw new Exception($"Record in index at position {position} is not a prepare");

				streamId = prepare.EventStreamId;
				return true;
			}
		}
	}
}
