namespace EventStore.Core.TransactionLog.Chunks.TFChunk
{
    public struct PosMap
    {
        public const int Size = sizeof(int) + sizeof(int);

        public readonly int LogPos;
        public readonly int ActualPos;

        public PosMap(ulong posmap)
        {
            LogPos = (int) (posmap >> 32);
            ActualPos = (int) (posmap & 0xFFFFFFFF);
        }

        public PosMap(int logPos, int actualPos)
        {
            LogPos = logPos;
            ActualPos = actualPos;
        }

        public ulong AsUInt64()
        {
            return (((ulong)LogPos) << 32) | (uint)ActualPos;
        }

        public override string ToString()
        {
            return string.Format("LogPos: {0}, ActualPos: {1}", LogPos, ActualPos);
        }
    }
}