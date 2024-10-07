// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data;

public enum ReadStreamResult {
	Success = 0,
	NoStream = 1,
	StreamDeleted = 2,
	NotModified = 3,
	Error = 4,
	AccessDenied = 5,
	Expired = 6,
}
