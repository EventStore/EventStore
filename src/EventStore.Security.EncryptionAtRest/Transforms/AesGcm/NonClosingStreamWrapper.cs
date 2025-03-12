// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;

namespace EventStore.Security.EncryptionAtRest.Transforms.AesGcm;


public sealed class NonClosingStreamWrapper(Stream innerStream) : Stream {
	public override void Flush() => innerStream.Flush();
	public override int Read(byte[] buffer, int offset, int count) => innerStream.Read(buffer, offset, count);
	public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);
	public override void SetLength(long value) => innerStream.SetLength(value);
	public override void Write(byte[] buffer, int offset, int count) => innerStream.Write(buffer, offset, count);

	public override bool CanRead => innerStream.CanRead;
	public override bool CanSeek => innerStream.CanSeek;
	public override bool CanWrite => innerStream.CanWrite;
	public override long Length => innerStream.Length;
	public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

	protected override void Dispose(bool disposing) {
		// we don't dispose the inner stream
	}
}
