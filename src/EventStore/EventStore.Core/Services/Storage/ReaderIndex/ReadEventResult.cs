using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct ReadEventResult
    {
        public readonly SingleReadResult Result;
        public readonly EventRecord Record;

        public ReadEventResult(SingleReadResult result)
        {
            if (result == SingleReadResult.Success)
                throw new ArgumentException(string.Format("Wrong SingleReadResult provided for failure constructor: {0}.", result), "result");

            Result = result;
            Record = null;
        }

        public ReadEventResult(SingleReadResult result, EventRecord record)
        {
            Result = result;
            Record = record;
        }
    }
}