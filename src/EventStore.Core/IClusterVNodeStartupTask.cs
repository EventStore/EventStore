// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core;

// Implementations of this interface are run to completion during startup
public interface IClusterVNodeStartupTask {
	ValueTask Run(CancellationToken token);
}
