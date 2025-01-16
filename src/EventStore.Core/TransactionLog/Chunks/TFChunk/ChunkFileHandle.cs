// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

internal sealed class ChunkFileHandle : Disposable, IChunkHandle {
	private readonly SafeFileHandle _handle;

	public ChunkFileHandle(string path, FileStreamOptions options) {
		Debug.Assert(options is not null);
		Debug.Assert(path is { Length: > 0 });

		_handle = File.OpenHandle(path, options.Mode, options.Access, options.Share, options.Options,
			options.PreallocationSize);
		Access = options.Access;
	}

	internal static FileOptions ConvertToFileOptions(IChunkFileSystem.ReadOptimizationHint optimizationHint) => optimizationHint switch {
		IChunkFileSystem.ReadOptimizationHint.RandomAccess => FileOptions.Asynchronous | FileOptions.RandomAccess,
		IChunkFileSystem.ReadOptimizationHint.SequentialScan => FileOptions.Asynchronous | FileOptions.SequentialScan,
		_ => FileOptions.Asynchronous,
	};

	public void Flush() => RandomAccess.FlushToDisk(_handle);

	public ValueTask WriteAsync(ReadOnlyMemory<byte> data, long offset, CancellationToken token)
		=> RandomAccess.WriteAsync(_handle, data, offset, token);

	public ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token)
		=> RandomAccess.ReadAsync(_handle, buffer, offset, token);

	public long Length {
		get => RandomAccess.GetLength(_handle);
		set => RandomAccess.SetLength(_handle, value);
	}

	public FileAccess Access { get; }

	protected override void Dispose(bool disposing) {
		if (disposing) {
			_handle.Dispose();
		}

		base.Dispose(disposing);
	}
}
