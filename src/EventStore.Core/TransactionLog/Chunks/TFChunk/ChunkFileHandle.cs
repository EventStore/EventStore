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

internal sealed class ChunkFileHandle : Disposable, IChunkHandleWithSync {
	const bool AsynchronousByDefault = false;

	private readonly SafeFileHandle _handle;
	private readonly string _path;
	// determines whether ReadAsync/WriteAsync are forced to run synchronously
	private readonly bool _asynchronous;

	public ChunkFileHandle(string path, FileStreamOptions options, bool asynchronous = AsynchronousByDefault) {
		Debug.Assert(options is not null);
		Debug.Assert(path is { Length: > 0 });

		var fileOptions = asynchronous
			? options.Options | FileOptions.Asynchronous
			: options.Options;

		_handle = File.OpenHandle(path, options.Mode, options.Access, options.Share, fileOptions,
			options.PreallocationSize);
		Access = options.Access;
		_path = path;
		_asynchronous = asynchronous;
	}

	// UnbufferedStreamWithSync makes use of the synchronous Read/Write methods on the handle only
	// when the synchronous Read/Write methods of the stream are called.
	// It is suitable regardless of the value of _asynchronous
	public Stream CreateStream(bool leaveOpen = true) => new UnbufferedStreamWithSync(this, leaveOpen);

	private sealed class UnbufferedStreamWithSync(IChunkHandleWithSync handle, bool leaveOpen)
		: IChunkHandle.UnbufferedStream(handle, leaveOpen) {

		protected override void Write(ReadOnlySpan<byte> buffer, long offset) =>
			handle.Write(buffer, offset);

		protected override int Read(Span<byte> buffer, long offset) =>
			handle.Read(buffer, offset);
	}

	internal static FileOptions ConvertToFileOptions(IChunkFileSystem.ReadOptimizationHint optimizationHint) => optimizationHint switch {
		IChunkFileSystem.ReadOptimizationHint.RandomAccess => FileOptions.RandomAccess,
		IChunkFileSystem.ReadOptimizationHint.SequentialScan => FileOptions.SequentialScan,
		_ => FileOptions.None,
	};

	public string Name => _path;

	public void Flush() => RandomAccess.FlushToDisk(_handle);

	public ValueTask WriteAsync(ReadOnlyMemory<byte> data, long offset, CancellationToken token) {
		var ret = ValueTask.CompletedTask;
		if (_asynchronous) {
			ret = RandomAccess.WriteAsync(_handle, data, offset, token);
		} else {
			try {
				Write(data.Span, offset);
			} catch (Exception ex) {
				ret = ValueTask.FromException(ex);
			}
		}
		return ret;
	}

	public ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token) {
		ValueTask<int> ret;
		if (_asynchronous) {
			ret = RandomAccess.ReadAsync(_handle, buffer, offset, token);
		} else {
			try {
				ret = new(Read(buffer.Span, offset));
			} catch (Exception ex) {
				ret = ValueTask.FromException<int>(ex);
			}
		}
		return ret;
	}

	public void Write(ReadOnlySpan<byte> data, long offset) =>
		RandomAccess.Write(_handle, data, offset);

	public int Read(Span<byte> buffer, long offset) =>
		RandomAccess.Read(_handle, buffer, offset);

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
