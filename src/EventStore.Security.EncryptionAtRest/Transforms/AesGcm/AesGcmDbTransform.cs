// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Plugins.Transforms;

namespace EventStore.Security.EncryptionAtRest.Transforms.AesGcm;

public sealed class AesGcmDbTransform : IDbTransform {
	public string Name => "aes-gcm";
	public TransformType Type => TransformType.Encryption_AesGcm;
	public IChunkTransformFactory ChunkFactory { get; }

	private readonly int[] _keySizesInBits = { 128, 192, 256 };

	public AesGcmDbTransform(IReadOnlyList<MasterKey> masterKeys, int keySizeInBits) {
		ArgumentOutOfRangeException.ThrowIfZero(masterKeys.Count);

		if (!_keySizesInBits.Contains(keySizeInBits))
			throw new ArgumentOutOfRangeException(nameof(keySizeInBits), $"Key size not supported: {keySizeInBits}");

		ChunkFactory = new AesGcmChunkTransformFactory(masterKeys, keySizeInBits / 8);
	}
}
