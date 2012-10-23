namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public enum SingleReadResult
    {
        Success,
        NotFound,
        NoStream,
        StreamDeleted
    }
}