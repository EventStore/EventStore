using System.IO;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk
{
    public static class PosMapVersion {
        public const byte PosMapV3 = 3;
        public const byte PosMapV2 = 2;
        public const byte PosMapV1 = 1;
    }

    public struct PosMap
    {
        public const byte CurrentPosMapVersion = PosMapVersion.PosMapV2;

        public const int V3Size = sizeof(long) + sizeof(int) + sizeof(int);
        public const int V2Size = sizeof(long) + sizeof(int);
        public const int V1Size = sizeof(int) + sizeof(int);

        public readonly long LogPos;
        public readonly int ActualPos;
        public readonly int LengthOffset;

        public PosMap(long logPos, int actualPos, int lengthOffset = 0)
        {
            LogPos = logPos;
            ActualPos = actualPos;
            LengthOffset = lengthOffset;
        }

        public static PosMap FromV3Format(BinaryReader reader)
        {
            var actualPos = reader.ReadInt32();
            var logPos = reader.ReadInt64();
            var lengthOffset = reader.ReadInt32();
            return new PosMap(logPos, actualPos, lengthOffset);
        }
        
        public static PosMap FromV2Format(BinaryReader reader)
        {
            var actualPos = reader.ReadInt32();
            var logPos = reader.ReadInt64();
            return new PosMap(logPos, actualPos);
        }

        public static PosMap FromV1Format(BinaryReader reader)
        {
            var posmap = reader.ReadUInt64();
            var logPos = (int)(posmap >> 32);
            var actualPos = (int)(posmap & 0xFFFFFFFF);
            return new PosMap(logPos, actualPos);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(ActualPos);
            writer.Write(LogPos);
        }

        public override string ToString()
        {
            return string.Format("LogPos: {0}, ActualPos: {1}, LengthOffset: {2}", LogPos, ActualPos, LengthOffset);
        }
    }
}