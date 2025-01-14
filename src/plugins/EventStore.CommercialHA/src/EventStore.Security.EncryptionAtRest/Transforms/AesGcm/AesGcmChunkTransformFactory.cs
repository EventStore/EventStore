// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using EventStore.Plugins.Transforms;
using EventStore.Security.EncryptionAtRest.Transforms.Common;

namespace EventStore.Security.EncryptionAtRest.Transforms.AesGcm;

using BlockInfo = AesGcmBlockInfo;

public sealed class AesGcmChunkTransformFactory(IReadOnlyList<MasterKey> masterKeys, int keySize) : IChunkTransformFactory {
	private const int ChunkHeaderSize = 128;
	private const int TransformHeaderSize = EncryptionHeader.Size + AesGcmHeader.Size;

	static AesGcmChunkTransformFactory() {
		ArgumentOutOfRangeException.ThrowIfLessThan(TransformHeaderSize, EncryptionHeader.Size + AesGcmHeader.Size);
	}

	public TransformType Type => TransformType.Encryption_AesGcm;

	public int TransformDataPosition(int dataPosition) {
		var numBlocks = dataPosition / BlockInfo.DataSize;
		if (dataPosition % BlockInfo.DataSize != 0)
			numBlocks++;

		return TransformHeaderSize + numBlocks * BlockInfo.BlockSize;
	}

	public ReadOnlyMemory<byte> CreateTransformHeader() {
		var transformHeader = new byte[TransformHeaderSize].AsMemory();
		var header = transformHeader.Span;

		var encryptionHeader = header[..EncryptionHeader.Size];
		EncryptionHeader.Write(encryptionHeader, EncryptionHeader.CurrentVersion, masterKeys[^1]);
		header = header[EncryptionHeader.Size..];

		var aesGcmHeader = header[..AesGcmHeader.Size];
		AesGcmHeader.Write(aesGcmHeader, AesGcmHeader.CurrentVersion, keySize * 8);

		return transformHeader;
	}

	public ReadOnlyMemory<byte> ReadTransformHeader(Stream stream) {
		var header = new byte[TransformHeaderSize];
		stream.ReadExactly(header);
		return header;
	}

	public IChunkTransform CreateTransform(ReadOnlyMemory<byte> transformHeader) {
		var header = transformHeader.Span;

		var encryptionHeader = header[..EncryptionHeader.Size];
		EncryptionHeader.Read(encryptionHeader, out _, out var masterKeyId, out var salt);
		var masterKey = masterKeys.FirstOrDefault(x => x.Id == masterKeyId);
		if (masterKey == default)
			throw new Exception($"Failed to load master key with ID: {masterKeyId}");
		header = header[EncryptionHeader.Size..];

		var aesGcmHeader = header[..AesGcmHeader.Size];
		AesGcmHeader.Read(aesGcmHeader, out _, out var aesKeySizeInBits);
		var dataKey = DeriveDataKey(
			dataKeySize: aesKeySizeInBits / 8,
			masterKey: masterKey.Key.Span,
			salt: salt);

		return new AesGcmChunkTransform(dataKey, TransformHeaderSize);
	}

	private static ReadOnlyMemory<byte> DeriveDataKey(int dataKeySize, ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> salt) {
		var dataKey = new byte[dataKeySize].AsMemory();

		// see: https://soatok.blog/2021/11/17/understanding-hkdf/ about why passing the salt to the info parameter
		// is important to have "KDF security" guarantees.
		HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, dataKey.Span, "AesGcm-Chunk"u8, salt);
		return dataKey;
	}
}
