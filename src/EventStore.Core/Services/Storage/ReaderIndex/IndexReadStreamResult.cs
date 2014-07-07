using System;
using EventStore.Common.Utils;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct IndexReadStreamResult
    {
        internal static readonly EventRecord[] EmptyRecords = new EventRecord[0];

        public readonly int FromEventNumber;
        public readonly int MaxCount;

        public readonly ReadStreamResult Result;
        public readonly int NextEventNumber;
        public readonly int LastEventNumber;
        public readonly bool IsEndOfStream;

        public readonly EventRecord[] Records;
        public readonly StreamMetadata Metadata;

        public IndexReadStreamResult(int fromEventNumber, int maxCount, ReadStreamResult result, StreamMetadata metadata, int lastEventNumber)
        {
            if (result == ReadStreamResult.Success)
                throw new ArgumentException(String.Format("Wrong ReadStreamResult provided for failure constructor: {0}.", result), "result");

            FromEventNumber = fromEventNumber;
            MaxCount = maxCount;

            Result = result;
            NextEventNumber = -1;
            LastEventNumber = lastEventNumber;
            IsEndOfStream = true;
            Records = EmptyRecords;
            Metadata = metadata;
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
            return String.Format("FromEventNumber: {0}, Maxcount: {1}, Result: {2}, Record count: {3}, Metadata: {4}, " 
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