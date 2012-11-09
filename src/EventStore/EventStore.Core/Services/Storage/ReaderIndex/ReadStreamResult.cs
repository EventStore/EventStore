using System;
using EventStore.Common.Utils;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct ReadStreamResult
    {
        public readonly RangeReadResult Result;
        public readonly int NextEventNumber;
        public readonly int LastEventNumber;
        public readonly bool IsEndOfStream;

        public readonly EventRecord[] Records;

        public ReadStreamResult(RangeReadResult result)
        {
            if (result == RangeReadResult.Success)
                throw new ArgumentException(string.Format("Wrong RangeReadResult provided for failure constructor: {0}.", result), "result");

            Result = result;
            NextEventNumber = -1;
            LastEventNumber = -1;
            IsEndOfStream = false;
            Records = ReadIndex.EmptyRecords;
        }

        public ReadStreamResult(RangeReadResult result, EventRecord[] records, int nextEventNumber, int lastEventNumber, bool isEndOfStream)
        {
            Ensure.NotNull(records, "records");

            Result = result;
            Records = records;
            NextEventNumber = nextEventNumber;
            LastEventNumber = lastEventNumber;
            IsEndOfStream = isEndOfStream;
        }

        public override string ToString()
        {
            return string.Format("Result: {0}, Record count: {1}, NextEventNumber: {2}, LastEventNumber: {3}, IsEndOfStream: {4}",
                                 Result,
                                 Records.Length,
                                 NextEventNumber,
                                 LastEventNumber,
                                 IsEndOfStream);
        }
    }
}