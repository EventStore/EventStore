// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;
using Serilog;

namespace EventStore.Core.Services.Storage;

// The resulting scavenger is used for one continuous run. If it is cancelled or
// completed then starting scavenge again will instantiate another scavenger
// with a different id.
public class ScavengerFactory(Func<ClientMessage.ScavengeDatabase, ITFChunkScavengerLog, ILogger, IScavenger> create) {
	public IScavenger Create(ClientMessage.ScavengeDatabase message, ITFChunkScavengerLog scavengerLogger, ILogger logger) =>
		create(message, scavengerLogger, logger);
}
