using System;

namespace EventStore.ClientAPI.Defines
{
    //TODO GFY THIS SHOULD NOT BE PUBLIC
    [Flags]
    public enum PrepareFlags : ushort
    {
        None = 0x00,
        Data = 0x01,                // prepare contains data
        TransactionBegin = 0x02,    // prepare starts transaction
        TransactionEnd = 0x04,      // prepare ends transaction
        StreamDelete = 0x08,        // prepare deletes stream

        IsCommited = 0x10,          // prepare should be considered committed immediately, no commit will follow in TF
        //Snapshot = 0x20,          // prepare belongs to snapshot stream, only last event in stream will be kept after scavenging

        //Update = 0x80,            // prepare updates previous instance of the same event, DANGEROUS!
        IsJson = 0x100,             // indicates data & metadata are valid json

        // aggregate flag set
        DeleteTombstone = TransactionBegin | TransactionEnd | StreamDelete,
        SingleWrite = Data | TransactionBegin | TransactionEnd
    }
}