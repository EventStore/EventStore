// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers.Binary;
using System.Diagnostics;

namespace EventStore.Security.EncryptionAtRest.Transforms.AesGcm;

using AesGcm = System.Security.Cryptography.AesGcm;
using BlockInfo = AesGcmBlockInfo;

public sealed class AesGcmWriteBlock : IDisposable {
	private readonly AesGcm _aesGcm;

	private readonly byte[] _block;
	private readonly Memory<byte> _header;
	private readonly Memory<byte> _data;
	private readonly Memory<byte> _tag;

	private const int NonceSize = 12;
	private readonly Memory<byte> _nonce = new byte[NonceSize];

	private readonly Action<byte[], bool> _writeBlock;

	public AesGcmWriteBlock(AesGcm aesGcm, Action<byte[], bool> writeBlock) {
		ArgumentNullException.ThrowIfNull(aesGcm);
		ArgumentNullException.ThrowIfNull(writeBlock);

		_aesGcm = aesGcm;
		_writeBlock = writeBlock;

		_block = new byte[BlockInfo.BlockSize];

		var block = _block.AsMemory();

		_header = block[..BlockInfo.HeaderSize];
		block = block[BlockInfo.HeaderSize..];

		_data = block[..BlockInfo.DataSize];
		block = block[BlockInfo.DataSize..];

		_tag = block[..BlockInfo.TagSize];
		block = block[BlockInfo.TagSize..];

		Debug.Assert(block.Length == 0);
	}

	public void Write(int blockNumber, ReadOnlySpan<byte> plaintext, bool updateChecksum) {
		var dataSize = plaintext.Length;
		BinaryPrimitives.WriteUInt16LittleEndian(_header.Span, (ushort) dataSize);
		CalcNonce(blockNumber, dataSize);
		Encrypt(plaintext, dataSize);
		_writeBlock(_block, updateChecksum);
	}

	private void Encrypt(ReadOnlySpan<byte> plaintext, int dataSize) {
		// note: for performance reasons, we do not clear the unused portion of _data with zeroes.
		// so, some ciphertext from the previous block may be present after the ciphertext of the current block.
		// security-wise, it doesn't matter as we have already written the previous ciphertext block to disk,
		// so we're not leaking any new information.

		_aesGcm.Encrypt(_nonce.Span, plaintext, _data.Span[..dataSize], _tag.Span);
	}

	private void CalcNonce(int blockNumber, int dataSize) {
		var uniquePosition = (long) blockNumber * BlockInfo.DataSize + dataSize;
		BinaryPrimitives.WriteInt64LittleEndian(_nonce.Span, uniquePosition);
	}

	public void Dispose() {
		_aesGcm.Dispose();
	}
}
