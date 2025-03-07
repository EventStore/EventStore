// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Plugins.Transforms;

namespace EventStore.Security.EncryptionAtRest.Transforms.AesGcm;

public sealed class AesGcmChunkTransform : IChunkTransform {
	public IChunkReadTransform Read { get; }
	public IChunkWriteTransform Write { get; }

	public AesGcmChunkTransform(ReadOnlyMemory<byte> key, int transformHeaderSize) {
		ArgumentNullException.ThrowIfNull(key);
		ArgumentOutOfRangeException.ThrowIfNegative(transformHeaderSize);

		Read = new AesGcmChunkReadTransform(key, transformHeaderSize);
		Write = new AesGcmChunkWriteTransform(key, transformHeaderSize, Read);
	}
}
