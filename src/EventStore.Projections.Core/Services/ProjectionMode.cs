// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Projections.Core.Services;

public enum ProjectionMode {
	Transient = 0,
	OneTime = 1,

	//____1 = 1,
	//____2 = 2,
	//____3 = 3,
	Continuous = 4,
	AllNonTransient = 999,
}
