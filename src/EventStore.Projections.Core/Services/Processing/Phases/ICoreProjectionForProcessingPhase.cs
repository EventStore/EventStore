// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Projections.Core.Services.Processing.Checkpointing;
using EventStore.Projections.Core.Services.Processing.WorkItems;

namespace EventStore.Projections.Core.Services.Processing.Phases;

public interface ICoreProjectionForProcessingPhase {
	void CompletePhase();
	void SetFaulted(string reason);
	void SetFaulted(Exception ex);
	void SetFaulting(string reason);
	void SetCurrentCheckpointSuggestedWorkItem(CheckpointSuggestedWorkItem checkpointSuggestedWorkItem);
	void EnsureTickPending();
	CheckpointTag LastProcessedEventPosition { get; }
	void Subscribed();
}
