// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using EventStore.Plugins.Transforms;
using EventStore.Security.EncryptionAtRest.Transforms.DataStructures;

namespace EventStore.Security.EncryptionAtRest.Transforms.AesGcm;

using AesGcm = System.Security.Cryptography.AesGcm;
using BlockInfo = AesGcmBlockInfo;

public sealed class AesGcmChunkReadStream : ChunkDataReadStream {
	private const int ChunkHeaderSize = 128;

	private readonly AesGcmReadBlock _block;
	private readonly SieveBlockCache _blockCache;

	private int _curBlockNumber;

	private int BlockSize { get; }
	private int DataSize { get; }
	private int TransformHeaderSize { get; }

	// plaintext of the last read block
	private readonly Memory<byte> _lastBlock;

	// size of the plaintext of the last read block
	private int _lastBlockDataSize;

	// position that the stream has reached within the last plaintext block
	private int _lastBlockPos;

	// keeps track of the base stream's position. seeks are done lazily to optimize performance.
	private long _basePosition;

	public AesGcmChunkReadStream(
		ChunkDataReadStream stream,
		ReadOnlySpan<byte> key,
		int transformHeaderSize) :
		base(stream.ChunkFileStream) {
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentOutOfRangeException.ThrowIfNegative(transformHeaderSize);

		var aesGcm = new AesGcm(key, BlockInfo.TagSize);
		_block = new AesGcmReadBlock(aesGcm, ReadFileStream);
		_blockCache = new SieveBlockCache(cacheSize: 15, blockDataSize: BlockInfo.DataSize);
		_curBlockNumber = 0;

		BlockSize = BlockInfo.BlockSize;
		DataSize = BlockInfo.DataSize;
		TransformHeaderSize = transformHeaderSize;

		_lastBlock = new byte[BlockInfo.DataSize];
		_lastBlockDataSize = DataSize;
		_lastBlockPos = 0;

		_basePosition = base.Position;
	}

	public override int Read(byte[] bufferArray, int offset, int count) {
		var buffer = bufferArray.AsSpan()[offset..(offset + count)];

		// if the last block we've read was incomplete and the read is requesting for more data than it contains,
		// then read the last block again. note that this code path is executed only when reading from the same stream
		// that we're writing to, e.g when using --mem-db on the server.
		if (_lastBlockDataSize < DataSize &&
		    _lastBlockDataSize - _lastBlockPos < count) {
			ReadLastBlockAgain();
		}

		// first, read data from the last plaintext block, if available, into the output buffer
		if (_lastBlockPos > 0) {
			var lastBlock = _lastBlock[_lastBlockPos.._lastBlockDataSize].Span;
			var numCopied = Copy(lastBlock, buffer);

			buffer = buffer[numCopied..];
			_lastBlockPos += numCopied;

			// if all data from the last plaintext block has been read, clear it
			if (_lastBlockPos == DataSize)
				ClearLastBlock();
		}

		// now, decrypt as many full blocks as possible directly into the output buffer
		while (buffer.Length >= DataSize) {
			ReadBlock(buffer[..DataSize]);
			buffer = buffer[DataSize..];
		}

		// finally, decrypt one last block and copy the required amount of data to the output buffer
		if (buffer.Length > 0) {
			ReadLastBlock();
			_lastBlockPos = Copy(_lastBlock.Span[.._lastBlockDataSize], buffer);
		}

		return count;
	}

	public override long Seek(long offset, SeekOrigin origin) {
		if (origin != SeekOrigin.Begin)
			throw new NotSupportedException();
		SetPosition(offset);
		return offset;
	}

	public override long Position {
		get => GetPosition();
		set => SetPosition(value);
	}

	protected override void Dispose(bool disposing) {
		try {
			if (!disposing)
				return;

			_block.Dispose();
		} finally {
			base.Dispose(disposing);
		}
	}

	public void PeekBlock(int blockNumber, Span<byte> plaintext) {
		var originalPos = _basePosition;
		var originalBlockNumber = _curBlockNumber;

		_curBlockNumber = blockNumber;
		ReadBlock(plaintext);

		_curBlockNumber = originalBlockNumber;
		_basePosition = originalPos;

		// immediately apply the pending seek as the base stream is used externally
		base.Position = _basePosition;
	}

	private static int Copy(ReadOnlySpan<byte> input, Span<byte> output) {
		var numToCopy = Math.Min(input.Length, output.Length);
		input[..numToCopy].CopyTo(output);
		return numToCopy;
	}

	private void ClearLastBlock() {
		_lastBlockPos = 0;
	}

	private int ReadBlock(Span<byte> plaintext) {
		var blockNumber = _curBlockNumber++;

		// first, try to obtain the block data from the cache
		if (_blockCache.TryGet(blockNumber, plaintext)) {
			_basePosition += BlockSize;
			return BlockInfo.DataSize; // we cache only complete blocks
		}

		// otherwise read the block from the base stream
		var dataSize = _block.Read(blockNumber, plaintext);

		if (dataSize == BlockInfo.DataSize) // cache only complete blocks
			_blockCache.Put(blockNumber, plaintext);

		return dataSize;
	}

	private void ReadLastBlock() {
		int retries = 20;
		do {
			try {
				_lastBlockDataSize = ReadBlock(_lastBlock.Span);
				break;
			} catch (AuthenticationTagMismatchException) {
				// the block was either:
				// i)  updated while we were reading it (this can usually happen when using --mem-db)
				// ii) really corrupted
				if (retries-- == 0)
					throw;

				// retry
				_basePosition -= BlockSize;
				_curBlockNumber--;
			}
		} while (true);
	}

	private void ReadLastBlockAgain() {
		_basePosition -= BlockSize;
		_curBlockNumber--;
		ReadLastBlock();
	}

	private void ReadFileStream(byte[] output) {
		// apply any pending seek before reading from the base stream
		base.Position = _basePosition;

		// note: we cannot use span-based APIs to read the base file stream here as we have
		// overridden: int Read(byte[] buffer, int offset, int count)

		var count = output.Length;
		var offset = 0;
		while (count > 0) {
			var read = base.Read(output, offset, count);
			if (read == 0)
				throw new EndOfStreamException();

			count -= read;
			offset += read;
		}

		_basePosition += output.Length;
	}

	private long GetPosition() {
		var position = UntransformPosition(_basePosition);
		if (_lastBlockPos > 0) {
			position -= DataSize; // we need to subtract DataSize as we had read one additional block
			position += _lastBlockPos;
		}
		return position;
	}

	private void SetPosition(long position) {
		var (blockNumber, blockStartPosition, remainder) = TransformPosition(position);

		// optimization to avoid a block read when we're already at the block we want to go to
		if (remainder > 0 &&
		    _lastBlockPos > 0 &&
		    _curBlockNumber == blockNumber + 1) {
			_lastBlockPos = remainder;

			// this line is required because we're using DotNext.IO.StreamSource.AsSharedStream() on the server and
			// not setting `Position` will cause it to have a value of zero.
			_basePosition = blockStartPosition + BlockSize;
			return;
		}

		ClearLastBlock();
		_curBlockNumber = blockNumber;
		_basePosition = blockStartPosition;
		if (remainder > 0) {
			ReadLastBlock();
			_lastBlockPos = remainder;
		}
	}

	private (int blockNumber, long blockStartPosition, int remainder) TransformPosition(long position) {
		position -= ChunkHeaderSize;
		var (blockNumber, remainder) = long.DivRem(position, DataSize);
		Debug.Assert(blockNumber <= int.MaxValue);
		Debug.Assert(remainder <= int.MaxValue);

		position = blockNumber * BlockSize;
		position += ChunkHeaderSize + TransformHeaderSize;
		return ((int)blockNumber, position, (int)remainder);
	}

	private long UntransformPosition(long blockStartPosition) {
		var position = blockStartPosition;

		position -= ChunkHeaderSize + TransformHeaderSize;
		var blockNumber = (int)(position / BlockSize);
		Debug.Assert(position % BlockSize == 0);
		position = blockNumber * DataSize;
		position += ChunkHeaderSize;

		return position;
	}
}
