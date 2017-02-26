using System;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class ChunkFooter
    {
        public const int Size = 128;
        public const int ChecksumSize = 16;

        // flags within single byte
        public readonly bool IsCompleted;
        public readonly byte MapVersion;
        private readonly bool IsUpgrade;

        public readonly int PhysicalDataSize; // the size of a section of data in chunk
        public readonly long LogicalDataSize;  // the size of a logical data size (after scavenge LogicalDataSize can be > physicalDataSize)
        public readonly int MapSize;
        public readonly byte[] MD5Hash;

        public readonly int MapCount; // calculated, not stored

        public ChunkFooter(bool isCompleted, bool isUpgrade, byte mapVersion, int physicalDataSize, long logicalDataSize, int mapSize, byte[] md5Hash)
        {
            Ensure.Nonnegative(physicalDataSize, "physicalDataSize");
            Ensure.Nonnegative(logicalDataSize, "logicalDataSize");
            if (logicalDataSize < physicalDataSize && !isUpgrade)
                throw new ArgumentOutOfRangeException("logicalDataSize", string.Format("LogicalDataSize {0} is less than PhysicalDataSize {1}", logicalDataSize, physicalDataSize));
            Ensure.Nonnegative(mapSize, "mapSize");
            Ensure.NotNull(md5Hash, "md5Hash");
            if (md5Hash.Length != ChecksumSize)
                throw new ArgumentException("MD5Hash is of wrong length.", "md5Hash");

            IsCompleted = isCompleted;
            MapVersion = mapVersion;
            IsUpgrade = isUpgrade;

            PhysicalDataSize = physicalDataSize;
            LogicalDataSize = logicalDataSize;
            MapSize = mapSize;
            MD5Hash = md5Hash;

            var posMapSize = PosMap.V3Size;
            if(MapVersion == PosMapVersion.PosMapV2) {
                posMapSize = PosMap.V2Size;
            }
            else if(MapVersion == PosMapVersion.PosMapV1) {
                posMapSize = PosMap.V1Size;
            }
            if (MapSize % posMapSize != 0)
                throw new Exception(string.Format("Wrong MapSize {0} -- not divisible by PosMap.Size {1}.", MapSize, posMapSize));
            MapCount = mapSize / posMapSize;
        }

        public byte[] AsByteArray()
        {
            var array = new byte[Size];
            using (var memStream = new MemoryStream(array))
            using (var writer = new BinaryWriter(memStream))
            {
                var flags = (byte) ((IsCompleted ? 1 : 0) |
                                    (MapVersion == PosMapVersion.PosMapV2 ? 2 : 0) |
                                    (MapVersion == PosMapVersion.PosMapV3 ? 4 : 0) |
                                    (IsUpgrade ? 8 : 0));
                writer.Write(flags);
                writer.Write(PhysicalDataSize);
                if (MapVersion != PosMapVersion.PosMapV1)
                    writer.Write(LogicalDataSize);
                else
                    writer.Write((int)LogicalDataSize);
                writer.Write(MapSize);
                
                memStream.Position = Size - ChecksumSize;
                writer.Write(MD5Hash);
            }
            return array;
        }

        public static ChunkFooter FromStream(Stream stream)
        {
            var reader = new BinaryReader(stream);
            var flags = reader.ReadByte();
            var isCompleted = (flags & 1) != 0;
            var isMap12Bytes = (flags & 2) != 0;
            var isMap16Bytes = (flags & 4) != 0;
            var isUpgrade = (flags & 8) != 0;
            var physicalDataSize = reader.ReadInt32();
            var logicalDataSize = isMap12Bytes || isMap16Bytes ? reader.ReadInt64() : reader.ReadInt32();
            var mapSize = reader.ReadInt32();

            stream.Seek(-ChecksumSize, SeekOrigin.End);
            var hash = reader.ReadBytes(ChecksumSize);

            var version = PosMapVersion.PosMapV1;
            if(isMap12Bytes) {
                version = PosMapVersion.PosMapV2;
            } else if(isMap16Bytes) {
                version = PosMapVersion.PosMapV3;
            }
            return new ChunkFooter(isCompleted, isUpgrade, version, physicalDataSize, logicalDataSize, mapSize, hash);
        }
    }
}