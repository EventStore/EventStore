// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Plugins.Transforms;

namespace EventStore.Security.EncryptionAtRest.Transforms.AesGcm;

public sealed class AesGcmChunkReadTransform(ReadOnlyMemory<byte> key, int transformHeaderSize) : IChunkReadTransform {
	public ChunkDataReadStream TransformData(ChunkDataReadStream dataStream) {
		ArgumentNullException.ThrowIfNull(dataStream);
		return new AesGcmChunkReadStream(dataStream, key.Span, transformHeaderSize);
	}
}
