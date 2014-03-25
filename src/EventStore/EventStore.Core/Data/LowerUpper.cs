namespace EventStore.Core.Data
{
    public struct Range
    {
        public readonly int Lower;
        public readonly int Upper;

        public Range(int lower, int upper)
        {
            Lower = lower;
            Upper = upper;
        }
    }
}
