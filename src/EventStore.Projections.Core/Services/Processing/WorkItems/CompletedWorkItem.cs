// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Services.Processing.Phases;

namespace EventStore.Projections.Core.Services.Processing.WorkItems;

class CompletedWorkItem : CheckpointWorkItemBase {
	private readonly IProjectionPhaseCompleter _projection;

	public CompletedWorkItem(IProjectionPhaseCompleter projection)
		: base() {
		_projection = projection;
	}

	protected override void WriteOutput() {
		_projection.Complete();
		NextStage();
	}
}
