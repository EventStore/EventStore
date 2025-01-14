// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers.Binary;
using System.Diagnostics;

namespace EventStore.Security.EncryptionAtRest.Transforms.AesGcm;

using AesGcm = System.Security.Cryptography.AesGcm;
using BlockInfo = AesGcmBlockInfo;

public sealed class AesGcmReadBlock : IDisposable {
	private readonly AesGcm _aesGcm;

	private readonly byte[] _block;
	private readonly ReadOnlyMemory<byte> _header;
	private readonly ReadOnlyMemory<byte> _data;
	private readonly ReadOnlyMemory<byte> _tag;

	private const int NonceSize = 12;
	private readonly Memory<byte> _nonce = new byte[NonceSize];

	private readonly Action<byte[]> _readBlock;

	public AesGcmReadBlock(AesGcm aesGcm, Action<byte[]> readBlock) {
		ArgumentNullException.ThrowIfNull(aesGcm);
		ArgumentNullException.ThrowIfNull(readBlock);

		_aesGcm = aesGcm;
		_readBlock = readBlock;

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

	public int Read(int blockNumber, Span<byte> plaintext) {
		_readBlock(_block);
		var dataSize = BinaryPrimitives.ReadUInt16LittleEndian(_header.Span);
		CalcNonce(blockNumber, dataSize);
		Decrypt(dataSize, plaintext);
		return dataSize;
	}

	private void Decrypt(int dataSize, Span<byte> plaintext) {
		_aesGcm.Decrypt(_nonce.Span, _data.Span[..dataSize], _tag.Span, plaintext[..dataSize]);
	}

	private void CalcNonce(int blockNumber, int dataSize) {
		var uniquePosition = (long) blockNumber * BlockInfo.DataSize + dataSize;
		BinaryPrimitives.WriteInt64LittleEndian(_nonce.Span, uniquePosition);
	}

	public void Dispose() {
		_aesGcm.Dispose();
	}

}
