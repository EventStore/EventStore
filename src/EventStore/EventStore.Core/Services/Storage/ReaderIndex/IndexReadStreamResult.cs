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

        public IndexReadStreamResult(int fromEventNumber, int maxCount, ReadStreamResult result)
        {
            if (result == ReadStreamResult.Success)
                throw new ArgumentException(string.Format("Wrong ReadStreamResult provided for failure constructor: {0}.", result), "result");

            FromEventNumber = fromEventNumber;
            MaxCount = maxCount;

            Result = result;
            NextEventNumber = -1;
            LastEventNumber = -1;
            IsEndOfStream = false;
            Records = ReadIndex.EmptyRecords;
        }

        public IndexReadStreamResult(int fromEventNumber, 
                                int maxCount, 
                                ReadStreamResult result, 
                                EventRecord[] records, 
                                int nextEventNumber, 
                                int lastEventNumber, 
                                bool isEndOfStream)
        {
            Ensure.NotNull(records, "records");

            FromEventNumber = fromEventNumber;
            MaxCount = maxCount;

            Result = result;
            Records = records;
            NextEventNumber = nextEventNumber;
            LastEventNumber = lastEventNumber;
            IsEndOfStream = isEndOfStream;
        }

        public override string ToString()
        {
            return string.Format("FromEventNumber: {0}, Maxcount: {1}, Result: {2}, Record count: {3}, " 
                                 + "NextEventNumber: {4}, LastEventNumber: {5}, IsEndOfStream: {6}",
                                 FromEventNumber,
                                 MaxCount,
                                 Result,
                                 Records.Length,
                                 NextEventNumber,
                                 LastEventNumber,
                                 IsEndOfStream);
        }
    }
}