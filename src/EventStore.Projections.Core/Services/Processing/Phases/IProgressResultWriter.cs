// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Projections.Core.Services.Processing.Phases;

public interface IProgressResultWriter {
	void WriteProgress(float progress);
}
