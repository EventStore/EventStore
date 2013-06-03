using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct StreamCacheInfo
    {
        public readonly StreamMetadata Metadata;
        public readonly int? LastEventNumber;

        public StreamCacheInfo(int? lastEventNumber, StreamMetadata metadata)
        {
            LastEventNumber = lastEventNumber;
            Metadata = metadata;
        }
    }
}