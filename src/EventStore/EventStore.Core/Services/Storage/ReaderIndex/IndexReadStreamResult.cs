using System;
using EventStore.Common.Utils;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct IndexReadStreamResult
    {
        public readonly int FromEventNumber;
        public readonly int MaxCount;

        public readonly ReadStreamResult Result;
        public readonly int NextEventNumber;
        public readonly int LastEventNumber;
        public readonly bool IsEndOfStream;

        public readonly EventRecord[] Records;
        public readonly StreamMetadata Metadata;

        public IndexReadStreamResult(int fromEventNumber, int maxCount, ReadStreamResult result)
        {
            if (result == ReadStreamResult.Success)
                throw new ArgumentException(string.Format("Wrong ReadStreamResult provided for failure constructor: {0}.", result), "result");

            FromEventNumber = fromEventNumber;
            MaxCount = maxCount;

            Result = result;
            NextEventNumber = -1;
            LastEventNumber = -1;
            IsEndOfStream = true;
            Records = ReadIndex.EmptyRecords;
            Metadata = null;
        }

        public IndexReadStreamResult(int fromEventNumber, 
                                     int maxCount, 
                                     EventRecord[] records, 
                                     StreamMetadata metadata,
                                     int nextEventNumber, 
                                     int lastEventNumber, 
                                     bool isEndOfStream)
        {
            Ensure.NotNull(records, "records");

            FromEventNumber = fromEventNumber;
            MaxCount = maxCount;

            Result = ReadStreamResult.Success;
            Records = records;
            Metadata = metadata;
            NextEventNumber = nextEventNumber;
            LastEventNumber = lastEventNumber;
            IsEndOfStream = isEndOfStream;
        }

        public override string ToString()
        {
            return string.Format("FromEventNumber: {0}, Maxcount: {1}, Result: {2}, Record count: {3}, Metadata: {4}, " 
                                 + "NextEventNumber: {5}, LastEventNumber: {6}, IsEndOfStream: {7}",
                                 FromEventNumber,
                                 MaxCount,
                                 Result,
                                 Records.Length,
                                 Metadata,
                                 NextEventNumber,
                                 LastEventNumber,
                                 IsEndOfStream);
        }
    }
}