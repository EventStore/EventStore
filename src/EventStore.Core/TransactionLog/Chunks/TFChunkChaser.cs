using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks {
	public class TFChunkChaser : ITransactionFileChaser {
		public ICheckpoint Checkpoint {
			get { return _chaserCheckpoint; }
		}

		private readonly ICheckpoint _chaserCheckpoint;
		private readonly TFChunkReader _reader;

		public TFChunkChaser(TFChunkDb db, ICheckpoint writerCheckpoint, ICheckpoint chaserCheckpoint,
			bool optimizeReadSideCache) {
			Ensure.NotNull(db, "dbConfig");
			Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
			Ensure.NotNull(chaserCheckpoint, "chaserCheckpoint");

			_chaserCheckpoint = chaserCheckpoint;
			_reader = new TFChunkReader(db, writerCheckpoint, _chaserCheckpoint.Read(), optimizeReadSideCache);
		}

		public void Open() {
			// NOOP
		}

		public bool TryReadNext(out LogRecord record) {
			var res = TryReadNext();
			record = res.LogRecord;
			return res.Success;
		}

		public SeqReadResult TryReadNext() {
			var res = _reader.TryReadNext();
			if (res.Success)
				_chaserCheckpoint.Write(res.RecordPostPosition);
			else
				_chaserCheckpoint.Write(_reader.CurrentPosition);
			return res;
		}

		public void Dispose() {
			Close();
		}

		public void Close() {
			Flush();
		}

		public void Flush() {
			_chaserCheckpoint.Flush();
		}
	}
}
