namespace EventStore.Core.Data
{
    public static class EventNumber
    {
        public const long DeletedStream = int.MaxValue;
        public const long Invalid = long.MinValue;
    }
}