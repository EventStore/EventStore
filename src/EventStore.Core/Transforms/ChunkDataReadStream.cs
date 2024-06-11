using System;
using System.IO;

namespace EventStore.Core.Transforms;

public class ChunkDataReadStream(Stream chunkFileStream) : Stream {
	public Stream ChunkFileStream => chunkFileStream;

	public sealed override bool CanRead => true;
	public sealed override bool CanSeek => true;
	public sealed override bool CanWrite => false;
	public sealed override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException();
	public sealed override void Flush() => throw new InvalidOperationException();
	public sealed override void SetLength(long value) => throw new InvalidOperationException();
	public override long Length => throw new NotSupportedException();

	// reads must always return exactly `count` bytes as we never read past the (flushed) writer checkpoint
	public override int Read(byte[] buffer, int offset, int count) => ChunkFileStream.Read(buffer, offset, count);

	// seeks need to support only `SeekOrigin.Begin`
	public override long Seek(long offset, SeekOrigin origin) {
		if (origin != SeekOrigin.Begin)
			throw new NotSupportedException();

		return ChunkFileStream.Seek(offset, origin);
	}

	public override long Position {
		get => ChunkFileStream.Position;
		set => ChunkFileStream.Position = value;
	}

	protected override void Dispose(bool disposing) {
		try {
			if (!disposing)
				return;

			chunkFileStream.Dispose();
		} finally {
			base.Dispose(disposing);
		}
	}
}
