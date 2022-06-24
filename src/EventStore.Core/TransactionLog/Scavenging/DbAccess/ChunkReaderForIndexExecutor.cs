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
				if (!result.Success ||
					!(result.LogRecord is PrepareLogRecord prepare)) {

					streamId = default;
					return false;
				}

				streamId = prepare.EventStreamId;
				return true;
			}
		}
	}
}
