// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;

namespace EventStore.Projections.Core.Messages {
	public interface IProjectionCheckpointManager : IHandle<CoreProjectionProcessingMessage.ReadyForCheckpoint>,
		IHandle<CoreProjectionProcessingMessage.RestartRequested>,
		IHandle<CoreProjectionProcessingMessage.Failed> {
	}

	public interface IEmittedStreamContainer : IProjectionCheckpointManager,
		IHandle<CoreProjectionProcessingMessage.EmittedStreamAwaiting>,
		IHandle<CoreProjectionProcessingMessage.EmittedStreamWriteCompleted> {
	}
}
