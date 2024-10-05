// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data;

public enum ReadEventResult {
	Success = 0,
	NotFound = 1,
	NoStream = 2,
	StreamDeleted = 3,
	Error = 4,
	AccessDenied = 5
}
