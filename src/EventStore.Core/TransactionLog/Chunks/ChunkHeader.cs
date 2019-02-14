using System;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.Index;

namespace EventStore.Core.TransactionLog.Chunks {
	public class ChunkHeader {
		public const int Size = 128;

		public readonly long ChunkStartPosition; // return ChunkStartNumber * (long)ChunkSize;
		public readonly long ChunkEndPosition; // return (ChunkEndNumber + 1) * (long)ChunkSize;

		public readonly byte Version;
		public readonly int ChunkSize;
		public readonly int ChunkStartNumber;
		public readonly int ChunkEndNumber;
		public readonly bool IsScavenged; // uses 4 bytes (legacy)
		public readonly Guid ChunkId;

		public ChunkHeader(byte version, int chunkSize, int chunkStartNumber, int chunkEndNumber, bool isScavenged,
			Guid chunkId) {
			Ensure.Nonnegative(version, "version");
			Ensure.Positive(chunkSize, "chunkSize");
			Ensure.Nonnegative(chunkStartNumber, "chunkStartNumber");
			Ensure.Nonnegative(chunkEndNumber, "chunkEndNumber");
			if (chunkStartNumber > chunkEndNumber)
				throw new ArgumentOutOfRangeException("chunkStartNumber",
					"chunkStartNumber is greater than ChunkEndNumber.");

			Version = version;
			ChunkSize = chunkSize;
			ChunkStartNumber = chunkStartNumber;
			ChunkEndNumber = chunkEndNumber;
			IsScavenged = isScavenged;
			ChunkId = chunkId;

			ChunkStartPosition = ChunkStartNumber * (long)ChunkSize;
			ChunkEndPosition = (ChunkEndNumber + 1) * (long)ChunkSize;
		}

		public byte[] AsByteArray() {
			var array = new byte[Size];
			using (var memStream = new MemoryStream(array))
			using (var writer = new BinaryWriter(memStream)) {
				writer.Write((byte)FileType.ChunkFile);
				writer.Write(Version);
				writer.Write(ChunkSize);
				writer.Write(ChunkStartNumber);
				writer.Write(ChunkEndNumber);
				writer.Write(IsScavenged ? 1 : 0);
				writer.Write(ChunkId.ToByteArray());
			}

			return array;
		}

		public static ChunkHeader FromStream(Stream stream) {
			var reader = new BinaryReader(stream);

			var fileType = (FileType)reader.ReadByte();
			if (fileType != FileType.ChunkFile)
				throw new CorruptDatabaseException(new InvalidFileException());

			var version = reader.ReadByte();
			var chunkSize = reader.ReadInt32();
			var chunkStartNumber = reader.ReadInt32();
			var chunkEndNumber = reader.ReadInt32();
			var isScavenged = reader.ReadInt32() > 0;
			var chunkId = new Guid(reader.ReadBytes(16));
			return new ChunkHeader(version, chunkSize, chunkStartNumber, chunkEndNumber, isScavenged, chunkId);
		}

		public long GetLocalLogPosition(long globalLogicalPosition) {
			if (globalLogicalPosition < ChunkStartPosition || globalLogicalPosition > ChunkEndPosition) {
				throw new Exception(string.Format(
					"globalLogicalPosition {0} is out of chunk logical positions [{1}, {2}].",
					globalLogicalPosition, ChunkStartPosition, ChunkEndPosition));
			}

			return globalLogicalPosition - ChunkStartPosition;
		}

		public override string ToString() {
			return string.Format(
				"Version: {0}, ChunkSize: {1}, ChunkStartNumber: {2}, ChunkEndNumber: {3}, IsScavenged: {4}, ChunkId: {5}\n" +
				"ChunkStartPosition: {6}, ChunkEndPosition: {7}, ChunkFullSize: {8}",
				Version,
				ChunkSize,
				ChunkStartNumber,
				ChunkEndNumber,
				IsScavenged,
				ChunkId,
				ChunkStartPosition,
				ChunkEndPosition,
				ChunkEndPosition - ChunkStartPosition);
		}
	}
}
