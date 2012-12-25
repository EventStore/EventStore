namespace EventStore.Core.Data
{
    public enum StreamResult
    {
        Success,
        NoStream,
        StreamDeleted,

        NotModified,
        Error
    }
}