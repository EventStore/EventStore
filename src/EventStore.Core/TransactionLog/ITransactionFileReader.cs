using System;
using EventStore.Core.DataStructures;

namespace EventStore.Core.TransactionLog {
	public interface ITransactionFileReader {
		void Reposition(long position);

		SeqReadResult TryReadNext(ITransactionFileTracker tracker);
		SeqReadResult TryReadPrev(ITransactionFileTracker tracker);

		RecordReadResult TryReadAt(long position, bool couldBeScavenged, ITransactionFileTracker tracker);
		bool ExistsAt(long position, ITransactionFileTracker tracker);
	}

	public readonly struct TFReaderLease : IDisposable {
		public readonly ITransactionFileReader Reader;
		private readonly ITransactionFileTracker _tracker;
		private readonly ObjectPool<ITransactionFileReader> _pool;

		public TFReaderLease(ObjectPool<ITransactionFileReader> pool, ITransactionFileTracker tracker) {
			_pool = pool;
			_tracker = tracker;
			Reader = pool.Get();
		}

		public TFReaderLease(ITransactionFileReader reader, ITransactionFileTracker tracker) {
			_pool = null;
			_tracker = tracker;
			Reader = reader;
		}

		void IDisposable.Dispose() {
			if (_pool != null) {
				_pool.Return(Reader);
			}
		}

		public void Reposition(long position) {
			Reader.Reposition(position);
		}

		public SeqReadResult TryReadNext() {
			return Reader.TryReadNext(_tracker);
		}

		public SeqReadResult TryReadPrev() {
			return Reader.TryReadPrev(_tracker);
		}

		public bool ExistsAt(long position) {
			return Reader.ExistsAt(position, _tracker);
		}

		public RecordReadResult TryReadAt(long position, bool couldBeScavenged) {
			return Reader.TryReadAt(position, couldBeScavenged, _tracker);
		}
	}
}
