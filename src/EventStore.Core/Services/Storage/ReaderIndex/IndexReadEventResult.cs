using System;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public struct IndexReadEventResult {
		public readonly ReadEventResult Result;
		public readonly EventRecord Record;
		public readonly StreamMetadata Metadata;
		public readonly long LastEventNumber;
		public readonly bool OriginalStreamExists;

		public IndexReadEventResult(ReadEventResult result, StreamMetadata metadata, long lastEventNumber,
			bool originalStreamExists) {
			if (result == ReadEventResult.Success)
				throw new ArgumentException(
					string.Format("Wrong ReadEventResult provided for failure constructor: {0}.", result), "result");

			Result = result;
			Record = null;
			Metadata = metadata;
			LastEventNumber = lastEventNumber;
			OriginalStreamExists = originalStreamExists;
		}

		public IndexReadEventResult(ReadEventResult result, EventRecord record, StreamMetadata metadata,
			long lastEventNumber, bool originalStreamExists) {
			Result = result;
			Record = record;
			Metadata = metadata;
			LastEventNumber = lastEventNumber;
			OriginalStreamExists = originalStreamExists;
		}
	}
}
