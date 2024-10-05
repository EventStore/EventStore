// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Cluster.Settings;

public static class ClusterConsts {
	public const int SubscriptionLastEpochCount = 20;
	public static readonly TimeSpan TruncationSyncTimeout = TimeSpan.FromSeconds(60);
}
