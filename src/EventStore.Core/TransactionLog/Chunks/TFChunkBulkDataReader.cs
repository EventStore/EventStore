using System;
using System.Diagnostics;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks;

public sealed class TFChunkBulkDataReader(TFChunk.TFChunk chunk, Stream streamToUse, bool isMemory)
	: TFChunkBulkReader(chunk, streamToUse, isMemory) {

	public override void SetPosition(long dataPosition) {
		var rawPos = dataPosition + ChunkHeader.Size;
		Stream.Position = rawPos;
	}

	public override BulkReadResult ReadNextBytes(int count, byte[] buffer) {
		Ensure.NotNull(buffer, "buffer");
		Ensure.Nonnegative(count, "count");

		if (Stream.Position == 0)
			Stream.Position = ChunkHeader.Size;

		if (count > buffer.Length)
			count = buffer.Length;

		var oldPos = (int)Stream.Position - ChunkHeader.Size;
		var toRead = Math.Min(Chunk.PhysicalDataSize - oldPos, count);
		Debug.Assert(toRead >= 0);
		Stream.Position = Stream.Position; // flush read buffer
		int bytesRead = Stream.Read(buffer, 0, toRead);
		return new BulkReadResult(oldPos,
			bytesRead,
			isEof: Chunk.IsReadOnly && oldPos + bytesRead == Chunk.PhysicalDataSize);
	}
}
