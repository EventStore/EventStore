// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

public interface IChunkWeightScavengeMap : IScavengeMap<int, float> {
	bool AllWeightsAreZero();
	void IncreaseWeight(int logicalChunkNumber, float extraWeight);
	float SumChunkWeights(int startLogicalChunkNumber, int endLogicalChunkNumber);
	void ResetChunkWeights(int startLogicalChunkNumber, int endLogicalChunkNumber);
}
