// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Services.Processing.Checkpointing;
using EventStore.Projections.Core.Services.Processing.Phases;

namespace EventStore.Projections.Core.Services.Processing.WorkItems;

class ProgressWorkItem : CheckpointWorkItemBase {
	private readonly ICoreProjectionCheckpointManager _checkpointManager;
	private readonly IProgressResultWriter _resultWriter;
	private readonly float _progress;

	public ProgressWorkItem(ICoreProjectionCheckpointManager checkpointManager, IProgressResultWriter resultWriter,
		float progress)
		: base(null) {
		_checkpointManager = checkpointManager;
		_resultWriter = resultWriter;
		_progress = progress;
	}

	protected override void WriteOutput() {
		_checkpointManager.Progress(_progress);
		_resultWriter.WriteProgress(_progress);
		NextStage();
	}
}
