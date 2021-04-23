using System;

//namespace EventStore.LogCommon { todo: fix namespace in a separate commit
namespace EventStore.Core.TransactionLog.LogRecords {
	[Flags]
	public enum PrepareFlags : ushort {
		None = 0x00,
		Data = 0x01, // prepare contains data
		TransactionBegin = 0x02, // prepare starts transaction
		TransactionEnd = 0x04, // prepare ends transaction
		StreamDelete = 0x08, // prepare deletes stream

		IsCommitted = 0x20, // prepare should be considered committed immediately, no commit will follow in TF
		//Update = 0x30,                  // prepare updates previous instance of the same event, DANGEROUS!

		IsJson = 0x100, // indicates data & metadata are valid json

		// aggregate flag set
		DeleteTombstone = TransactionBegin | TransactionEnd | StreamDelete,
		SingleWrite = Data | TransactionBegin | TransactionEnd
	}
}
