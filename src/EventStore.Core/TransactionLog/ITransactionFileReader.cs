using System;
using EventStore.Core.DataStructures;

namespace EventStore.Core.TransactionLog {
	public interface ITransactionFileReader {
		void OnCheckedOut(ITransactionFileTracker tracker);
		void OnReturned();
		void Reposition(long position);

		SeqReadResult TryReadNext();
		SeqReadResult TryReadPrev();

		RecordReadResult TryReadAt(long position, bool couldBeScavenged);
		bool ExistsAt(long position);
	}

	public readonly struct TFReaderLease : IDisposable {
		public readonly ITransactionFileReader Reader;
		private readonly ObjectPool<ITransactionFileReader> _pool;

		public TFReaderLease(ObjectPool<ITransactionFileReader> pool, ITransactionFileTracker tracker) {
			_pool = pool;
			Reader = pool.Get();
			Reader.OnCheckedOut(tracker);
		}

		public TFReaderLease(ITransactionFileReader reader) {
			_pool = null;
			Reader = reader;
			//qq what do we want to do about providing/clearing a tracker here?
		}

		void IDisposable.Dispose() {
			if (_pool != null) {
				Reader.OnReturned();
				_pool.Return(Reader);
			}
		}

		public void Reposition(long position) {
			Reader.Reposition(position);
		}

		public SeqReadResult TryReadNext() {
			return Reader.TryReadNext();
		}

		public SeqReadResult TryReadPrev() {
			return Reader.TryReadPrev();
		}

		public bool ExistsAt(long position) {
			return Reader.ExistsAt(position);
		}

		public RecordReadResult TryReadAt(long position, bool couldBeScavenged) {
			return Reader.TryReadAt(position, couldBeScavenged);
		}
	}
}
