using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct ReadAllResult
    {
        public readonly ResolvedEventRecord[] Records;
        public readonly int MaxCount;
        public readonly TFPos CurrentPos;
        public readonly TFPos NextPos;
        public readonly TFPos PrevPos;
        public readonly long TfEofPosition;

        public ReadAllResult(ResolvedEventRecord[] records, int maxCount, TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfEofPosition)
        {
            Ensure.NotNull(records, "records");

            Records = records;
            MaxCount = maxCount;
            CurrentPos = currentPos;
            NextPos = nextPos;
            PrevPos = prevPos;
            TfEofPosition = tfEofPosition;
        }

        public override string ToString()
        {
            return string.Format("NextPos: {0}, PrevPos: {1}, Records: {2}",
                                 NextPos,
                                 PrevPos,
                                 string.Join("\n", Records.Select(x => x.ToString())));
        }
    }
}