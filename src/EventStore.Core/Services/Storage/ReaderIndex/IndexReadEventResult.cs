using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct IndexReadEventResult
    {
        public readonly ReadEventResult Result;
        public readonly EventRecord Record;
        public readonly StreamMetadata Metadata;
        public readonly int LastEventNumber;

        public IndexReadEventResult(ReadEventResult result, StreamMetadata metadata, int lastEventNumber)
        {
            if (result == ReadEventResult.Success)
                throw new ArgumentException(string.Format("Wrong ReadEventResult provided for failure constructor: {0}.", result), "result");

            Result = result;
            Record = null;
            Metadata = metadata;
            LastEventNumber = lastEventNumber;
        }

        public IndexReadEventResult(ReadEventResult result, EventRecord record, StreamMetadata metadata, int lastEventNumber)
        {
            Result = result;
            Record = record;
            Metadata = metadata;
            LastEventNumber = lastEventNumber;
        }
    }
}