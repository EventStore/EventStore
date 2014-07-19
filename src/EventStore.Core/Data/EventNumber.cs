namespace EventStore.Core.Data
{
    public static class EventNumber
    {
        public const int DeletedStream = int.MaxValue;
        public const int Invalid = int.MinValue;
    }
}