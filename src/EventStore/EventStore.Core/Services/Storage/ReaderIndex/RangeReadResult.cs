namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public enum RangeReadResult
    {
        Success,
        NoStream,
        StreamDeleted
    }
}