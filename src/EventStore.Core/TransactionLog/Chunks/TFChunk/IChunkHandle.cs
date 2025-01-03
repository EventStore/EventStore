// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

/// <summary>
/// Represents a handle to access the underlying chunk physical storage.
/// </summary>
public interface IChunkHandle : IFlushable, IDisposable {

	ValueTask WriteAsync(ReadOnlyMemory<byte> data, long offset, CancellationToken token);

	ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token);

	/// <summary>
	/// Gets or sets the length of the data represented by this handle, in bytes.
	/// </summary>
	long Length {
		get;
		set;
	}

	/// <summary>
	/// Gets access mode for this handle.
	/// </summary>
	FileAccess Access { get; }

	ValueTask SetReadOnlyAsync(bool value, CancellationToken token);

	/// <summary>
	/// Creates an unbuffered stream for this handle.
	/// </summary>
	/// <returns>The unbuffered stream for this handle.</returns>
	Stream CreateStream() => CreateStream(this, 60_000);

	//qq maybe dont need the synchronous timeout?
	protected static Stream CreateStream(IChunkHandle handle, int synchronousTimeout)
		=> new UnbufferedStream(handle) { ReadTimeout = synchronousTimeout, WriteTimeout = synchronousTimeout };

	private sealed class UnbufferedStream(IChunkHandle handle) : RandomAccessStream {
		private int _readTimeout, _writeTimeout;

		public override void Flush() => handle.Flush();

		public override void SetLength(long value) => handle.Length = value;

		public override bool CanRead => handle.Access.HasFlag(FileAccess.Read);

		public override bool CanSeek => true;

		public override bool CanWrite => handle.Access.HasFlag(FileAccess.Write);

		public override bool CanTimeout => true;

		public override int WriteTimeout {
			get => _writeTimeout;
			set => _writeTimeout =
				value >= Timeout.Infinite ? value : throw new ArgumentOutOfRangeException(nameof(value));
		}

		public override int ReadTimeout {
			get => _readTimeout;
			set => _readTimeout =
				value >= Timeout.Infinite ? value : throw new ArgumentOutOfRangeException(nameof(value));
		}

		public override long Length => handle.Length;

		protected override void Write(ReadOnlySpan<byte> buffer, long offset) {
			throw new NotSupportedException("Call the async version instead");
		}

		protected override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, long offset, CancellationToken token)
			=> handle.WriteAsync(buffer, offset, token);

		protected override int Read(Span<byte> buffer, long offset) {
			throw new NotSupportedException("Call the async version instead");
		}

		protected override ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token)
			=> handle.ReadAsync(buffer, offset, token);
	}
}
