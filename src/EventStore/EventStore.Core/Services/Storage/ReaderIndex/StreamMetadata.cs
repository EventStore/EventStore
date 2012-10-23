using System;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct StreamMetadata
    {
        public readonly int? MaxCount;
        public readonly TimeSpan? MaxAge;

        public StreamMetadata(int? maxCount, TimeSpan? maxAge)
        {
            MaxCount = maxCount;
            MaxAge = maxAge;
        }
    }
}