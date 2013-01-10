using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct IndexReadEventResult
    {
        public readonly ReadEventResult Result;
        public readonly EventRecord Record;

        public IndexReadEventResult(ReadEventResult result)
        {
            if (result == ReadEventResult.Success)
                throw new ArgumentException(string.Format("Wrong ReadEventResult provided for failure constructor: {0}.", result), "result");

            Result = result;
            Record = null;
        }

        public IndexReadEventResult(ReadEventResult result, EventRecord record)
        {
            Result = result;
            Record = record;
        }
    }
}