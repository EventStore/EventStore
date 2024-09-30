// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Transforms;

namespace EventStore.Core.Transforms.Identity;

public sealed class IdentityDbTransform : IDbTransform {
	public string Name => "identity";
	public TransformType Type => TransformType.Identity;
	public IChunkTransformFactory ChunkFactory { get; } = new IdentityChunkTransformFactory();
}
