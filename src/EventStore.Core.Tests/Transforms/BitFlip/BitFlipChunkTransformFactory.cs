// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.Tests.Transforms.BitFlip;

public class BitFlipChunkTransformFactory : IChunkTransformFactory {
	public TransformType Type => (TransformType) 0xFF;
	public int TransformDataPosition(int dataPosition) => dataPosition;
	public void CreateTransformHeader(Span<byte> transformHeader) => transformHeader.Clear();
	public ValueTask ReadTransformHeader(Stream stream, Memory<byte> transformHeader, CancellationToken token)
		=> token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;

	public IChunkTransform CreateTransform(ReadOnlySpan<byte> transformHeader) => new BitFlipChunkTransform();

	public int TransformHeaderLength => 0;
}
