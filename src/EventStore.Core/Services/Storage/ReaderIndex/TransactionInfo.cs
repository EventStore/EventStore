namespace EventStore.Core.Services.Storage.ReaderIndex;

public readonly record struct TransactionInfo<TStreamId>(int TransactionOffset, TStreamId EventStreamId);
