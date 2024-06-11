using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.Index;
using DotNext.Buffers;
using DotNext.Buffers.Binary;
using EventStore.Core.Transforms;

namespace EventStore.Core.TransactionLog.Chunks {

	// TODO: Consider struct instead of class
	public sealed class ChunkHeader : IBinaryFormattable<ChunkHeader> {
		public const int Size = TFConsts.ChunkHeaderSize;

		public readonly long ChunkStartPosition; // return ChunkStartNumber * (long)ChunkSize;
		public readonly long ChunkEndPosition; // return (ChunkEndNumber + 1) * (long)ChunkSize;

		public readonly byte Version;
		public readonly int ChunkSize;
		public readonly int ChunkStartNumber;
		public readonly int ChunkEndNumber;
		public readonly bool IsScavenged; // uses 4 bytes (legacy)
		public readonly Guid ChunkId;
		public readonly TransformType TransformType;

		public ChunkHeader(byte version, int chunkSize, int chunkStartNumber, int chunkEndNumber, bool isScavenged,
			Guid chunkId, TransformType transformType) {
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
			TransformType = transformType;

			ChunkStartPosition = ChunkStartNumber * (long)ChunkSize;
			ChunkEndPosition = (ChunkEndNumber + 1) * (long)ChunkSize;
		}

		public ChunkHeader(ReadOnlySpan<byte> source) {
			Debug.Assert(source.Length >= Size);

			SpanReader<byte> reader = new(source);

			if ((FileType)reader.Read() is not FileType.ChunkFile)
				throw new CorruptDatabaseException(new InvalidFileException());

			Version = reader.Read();
			Debug.Assert(Version >= 0);

			ChunkSize = reader.ReadLittleEndian<int>();
			Debug.Assert(ChunkSize >= 0);

			ChunkStartNumber = reader.ReadLittleEndian<int>();
			Debug.Assert(ChunkStartNumber >= 0);

			ChunkEndNumber = reader.ReadLittleEndian<int>();
			Debug.Assert(ChunkEndNumber >= 0);

			IsScavenged = reader.ReadLittleEndian<int>() > 0;
			ChunkId = new(reader.Read(16));
			TransformType = (TransformType) reader.Read();

			ChunkStartPosition = ChunkStartNumber * (long)ChunkSize;
			ChunkEndPosition = (ChunkEndNumber + 1) * (long)ChunkSize;
		}

		static int IBinaryFormattable<ChunkHeader>.Size => Size;

		public void Format(Span<byte> destination) {
			Debug.Assert(destination.Length >= Size);

			SpanWriter<byte> writer = new(destination);
			writer.Add((byte)FileType.ChunkFile);
			writer.Add(Version);
			writer.WriteLittleEndian(ChunkSize);
			writer.WriteLittleEndian(ChunkStartNumber);
			writer.WriteLittleEndian(ChunkEndNumber);
			writer.WriteLittleEndian<int>(Unsafe.BitCast<bool, byte>(IsScavenged));

			Span<byte> guidBuffer = stackalloc byte[16];
			ChunkId.TryWriteBytes(guidBuffer);
			writer.Write(guidBuffer);

			writer.Add((byte)TransformType);
		}

		static ChunkHeader IBinaryFormattable<ChunkHeader>.Parse(ReadOnlySpan<byte> source)
			=> new(source);

		public byte[] AsByteArray() {
			var array = new byte[Size];
			Format(array);

			return array;
		}

		[SkipLocalsInit]
		public static ChunkHeader FromStream(Stream stream) {
			Span<byte> buffer = stackalloc byte[Size];
			stream.ReadExactly(buffer);
			return new(buffer);
		}

		public long GetLocalLogPosition(long globalLogicalPosition) {
			if (globalLogicalPosition < ChunkStartPosition || globalLogicalPosition > ChunkEndPosition) {
				throw new Exception(
					$"globalLogicalPosition {globalLogicalPosition} is out of chunk logical positions [{ChunkStartPosition}, {ChunkEndPosition}].");
			}

			return globalLogicalPosition - ChunkStartPosition;
		}

		public long GetGlobalLogPosition(long localLogicalPosition) {
			if (ChunkStartPosition + localLogicalPosition > ChunkEndPosition) {
				throw new Exception(
					$"localLogicalPosition {localLogicalPosition} is out of chunk logical positions [{ChunkStartPosition}, {ChunkEndPosition}].");
			}

			return ChunkStartPosition + localLogicalPosition;
		}

		public override string ToString() {
			return string.Format(
				"Version: {0}, ChunkSize: {1}, ChunkStartNumber: {2}, ChunkEndNumber: {3}, IsScavenged: {4}, ChunkId: {5}\n" +
				"TransformType: {6}, ChunkStartPosition: {7}, ChunkEndPosition: {8}, ChunkFullSize: {9}",
				Version,
				ChunkSize,
				ChunkStartNumber,
				ChunkEndNumber,
				IsScavenged,
				ChunkId,
				TransformType,
				ChunkStartPosition,
				ChunkEndPosition,
				ChunkEndPosition - ChunkStartPosition);
		}
	}
}
