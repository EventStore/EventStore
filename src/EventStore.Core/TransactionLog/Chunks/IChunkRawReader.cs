// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;

namespace EventStore.Core.TransactionLog.Chunks;

public interface IChunkRawReader : IDisposable {
	Stream Stream { get; }
}
