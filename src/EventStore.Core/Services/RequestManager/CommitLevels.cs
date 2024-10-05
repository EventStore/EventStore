// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.RequestManager;

public enum CommitLevel {
	Replicated, //Write on Cluster Quorum
	Indexed //Indexed on Leader
}
