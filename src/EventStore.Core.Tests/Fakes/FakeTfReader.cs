using System;
using EventStore.Core.TransactionLog;

namespace EventStore.Core.Tests.Fakes {
	public class FakeTfReader : ITransactionFileReader {
		public void Reposition(long position) {
			throw new NotImplementedException();
		}

		public SeqReadResult TryReadNext(ITransactionFileTracker tracker) {
			throw new NotImplementedException();
		}

		public SeqReadResult TryReadPrev(ITransactionFileTracker tracker) {
			throw new NotImplementedException();
		}

		public RecordReadResult TryReadAt(long position, bool couldBeScavenged, ITransactionFileTracker tracker) {
			throw new NotImplementedException();
		}

		public bool ExistsAt(long position, ITransactionFileTracker tracker) {
			return true;
		}
	}
}
