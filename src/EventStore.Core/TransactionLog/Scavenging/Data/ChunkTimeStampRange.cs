// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.TransactionLog.Scavenging;

// store a range per chunk so that the calculator can definitely get a timestamp range for each event
// that is guaranteed to contain the real timestamp of that event.
public readonly record struct ChunkTimeStampRange(DateTime Min, DateTime Max);
