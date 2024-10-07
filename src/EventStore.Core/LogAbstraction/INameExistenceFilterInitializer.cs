// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.LogAbstraction;

public interface INameExistenceFilterInitializer {
	void Initialize(INameExistenceFilter filter, long truncateToPosition);
}
