// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Chunks;

public record TFChunkInfo(string fileName);
public record LatestVersion(string fileName, int start, int end) : TFChunkInfo(fileName);
public record OldVersion(string fileName, int start) : TFChunkInfo(fileName);
public record MissingVersion(string fileName, int chunkNumber) : TFChunkInfo(fileName);
