using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Projections.Core.Services.Processing {
	internal struct PrepareInfo {
		private readonly PrepareLogRecord _prepareRecord;

		public PrepareInfo(PrepareLogRecord prepareRecord)
			: this() {
			_prepareRecord = prepareRecord;
		}

		public long Size {
			get { return PrepareRecord.InMemorySize; }
		}

		public PrepareLogRecord PrepareRecord {
			get { return _prepareRecord; }
		}
	}
}
