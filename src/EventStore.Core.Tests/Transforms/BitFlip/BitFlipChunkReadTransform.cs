// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.BitFlip;

public class BitFlipChunkReadTransform : IChunkReadTransform {
	public ChunkDataReadStream TransformData(ChunkDataReadStream dataStream) =>
		new BitFlipChunkReadStream(dataStream);
}
