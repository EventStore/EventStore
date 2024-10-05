// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Projections.Core.Services;

public static class ProjectionEventTypes {
	public const string ProjectionCheckpoint = "$ProjectionCheckpoint";
	public const string PartitionCheckpoint = "$Checkpoint";
	public const string StreamTracked = "$StreamTracked";

	public const string ProjectionCreated = "$ProjectionCreated";
	public const string ProjectionDeleted = "$ProjectionDeleted";
	public const string ProjectionsInitialized = "$ProjectionsInitialized";
	public const string ProjectionUpdated = "$ProjectionUpdated";
}
