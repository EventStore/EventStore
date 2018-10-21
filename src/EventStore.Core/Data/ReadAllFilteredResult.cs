namespace EventStore.Core.Data
{
    public enum ReadAllFilteredResult
    {
        Success = 0,
        NotModified = 1,
        Error = 2,
        AccessDenied = 3
    }
}