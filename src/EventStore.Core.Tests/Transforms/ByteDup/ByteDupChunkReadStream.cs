// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.ByteDup;

public class ByteDupChunkReadStream(ChunkDataReadStream stream)
	: ChunkDataReadStream(stream.ChunkFileStream) {
	private const int HeaderSize = 128;

	public override int Read(Span<byte> buffer) {
		var buf = new byte[buffer.Length * 2];
		int numRead = base.Read(buf);

		if (int.IsOddInteger(numRead))
			Debugger.Break();

		for (int i = 0; i < buffer.Length; i++)
			buffer[i] = buf[i * 2];

		return numRead / 2;
	}

	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) {
		var buf = new byte[buffer.Length * 2];
		int numRead = await base.ReadAsync(buf, token);

		if (int.IsOddInteger(numRead))
			Debugger.Break();

		for (int i = 0; i < buffer.Length; i++)
			buffer.Span[i] = buf[i * 2];

		return numRead / 2;
	}

	public override long Seek(long offset, SeekOrigin origin) {
		if (origin is not SeekOrigin.Begin)
			throw new NotSupportedException();

		Position = offset;
		return offset;
	}

	public override long Position {
		get => HeaderSize + (base.Position - HeaderSize) / 2L;
		set => base.Position = HeaderSize + (value - HeaderSize) * 2L;
	}
}
