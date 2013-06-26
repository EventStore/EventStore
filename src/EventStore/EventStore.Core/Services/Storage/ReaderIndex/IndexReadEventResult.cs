using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct IndexReadEventResult
    {
        public readonly ReadEventResult Result;
        public readonly EventRecord Record;
        public readonly StreamMetadata Metadata;

        public IndexReadEventResult(ReadEventResult result, StreamMetadata metadata)
        {
            if (result == ReadEventResult.Success)
                throw new ArgumentException(string.Format("Wrong ReadEventResult provided for failure constructor: {0}.", result), "result");

            Result = result;
            Record = null;
            Metadata = metadata;
        }

        public IndexReadEventResult(ReadEventResult result, EventRecord record, StreamMetadata metadata)
        {
            Result = result;
            Record = record;
            Metadata = metadata;
        }
    }
}