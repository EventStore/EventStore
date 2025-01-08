// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Chunks;

public abstract record TFChunkInfo(string FileName);
public record LatestVersion(string FileName, int Start, int End) : TFChunkInfo(FileName);
public record OldVersion(string FileName, int Start) : TFChunkInfo(FileName);
public record MissingVersion(string FileName, int Start) : TFChunkInfo(FileName);
