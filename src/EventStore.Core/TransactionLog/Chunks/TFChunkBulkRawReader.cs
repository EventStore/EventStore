using System;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks;

public sealed class TFChunkBulkRawReader(TFChunk.TFChunk chunk, Stream streamToUse, bool isMemory)
	: TFChunkBulkReader(chunk, streamToUse, isMemory) {

	public override void SetPosition(long rawPosition) {
		if (rawPosition >= Stream.Length)
			throw new ArgumentOutOfRangeException("rawPosition",
				string.Format("Raw position {0} is out of bounds.", rawPosition));
		Stream.Position = rawPosition;

	}

	public override BulkReadResult ReadNextBytes(int count, byte[] buffer) {
		Ensure.NotNull(buffer, "buffer");
		Ensure.Nonnegative(count, "count");

		if (count > buffer.Length)
			count = buffer.Length;

		var oldPos = (int)Stream.Position;
		int bytesRead = Stream.Read(buffer, 0, count);
		return new BulkReadResult(oldPos, bytesRead, isEof: Stream.Length == Stream.Position);
	}
}
