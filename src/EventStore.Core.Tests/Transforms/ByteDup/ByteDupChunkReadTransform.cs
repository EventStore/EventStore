// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.ByteDup;
public class ByteDupChunkReadTransform : IChunkReadTransform {
	public ChunkDataReadStream TransformData(ChunkDataReadStream dataStream) =>
		new ByteDupChunkReadStream(dataStream);
}
