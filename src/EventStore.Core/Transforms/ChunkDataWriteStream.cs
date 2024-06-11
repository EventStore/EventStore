using System;
using System.IO;
using System.Security.Cryptography;

namespace EventStore.Core.Transforms;

public class ChunkDataWriteStream(Stream chunkFileStream, HashAlgorithm checksumAlgorithm) : Stream {
	public Stream ChunkFileStream => chunkFileStream;
	public HashAlgorithm ChecksumAlgorithm => checksumAlgorithm;

	public sealed override bool CanRead => false;
	public sealed override bool CanSeek => false;
	public sealed override bool CanWrite => true;
	public sealed override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException();
	public sealed override long Seek(long offset, SeekOrigin origin) => throw new InvalidOperationException();

	public override void Write(byte[] buffer, int offset, int count) {
		ChunkFileStream.Write(buffer, offset, count);
		ChecksumAlgorithm.TransformBlock(buffer, 0, count, null, 0);
	}

	public override void Flush() => ChunkFileStream.Flush();
	public override void SetLength(long value) => ChunkFileStream.SetLength(value);
	public override long Length => ChunkFileStream.Length;
	public override long Position {
		get => ChunkFileStream.Position;
		set {
			if (ChunkFileStream.Position != 0)
				throw new InvalidOperationException("Writer's position can only be moved from 0 to a higher value.");

			ReadAndChecksum(value);

			if (ChunkFileStream.Position != value)
				throw new Exception($"Writer's position ({ChunkFileStream.Position:N0}) is not at the expected position ({value:N0})");
		}
	}

	private void ReadAndChecksum(long count) {
		var buffer = new byte[4096];
		long toRead = count;
		while (toRead > 0) {
			int read = ChunkFileStream.Read(buffer, 0, (int)Math.Min(toRead, buffer.Length));
			if (read == 0)
				break;

			ChecksumAlgorithm.TransformBlock(buffer, 0, read, null, 0);
			toRead -= read;
		}
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
