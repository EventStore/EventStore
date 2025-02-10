// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.POC.IO.Core;

public interface ISink {

	public Uri Config { get; }

	public Task Write(Event e, CancellationToken ct);
}
