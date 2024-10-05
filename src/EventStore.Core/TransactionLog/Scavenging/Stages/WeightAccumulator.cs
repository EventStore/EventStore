// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;

namespace EventStore.Core.TransactionLog.Scavenging;

public class WeightAccumulator {
	const float DiscardWeight = 2.0f;
	const float MaybeDiscardWeight = 1.0f;

	private readonly IIncreaseChunkWeights _state;
	private readonly Dictionary<int, float> _weights;

	public WeightAccumulator(IIncreaseChunkWeights state) {
		_state = state;
		_weights = new Dictionary<int, float>();
	}

	public void OnDiscard(int logicalChunkNumber) => IncreaseChunkWeight(
		logicalChunkNumber,
		DiscardWeight);

	public void OnMaybeDiscard(int logicalChunkNumber) => IncreaseChunkWeight(
		logicalChunkNumber,
		MaybeDiscardWeight);

	private void IncreaseChunkWeight(int logicalChunkNumber, float extraWeight) {
		if (_weights.TryGetValue(logicalChunkNumber, out var current)) {
			_weights[logicalChunkNumber] = extraWeight + current;
		} else {
			_weights[logicalChunkNumber] = extraWeight;
		}
	}

	public void Flush() {
		foreach (var kvp in _weights) {
			var logicalChunkNumber = kvp.Key;
			var extraWeight = kvp.Value;
			_state.IncreaseChunkWeight(logicalChunkNumber, extraWeight);
		}
		_weights.Clear();
	}
}
