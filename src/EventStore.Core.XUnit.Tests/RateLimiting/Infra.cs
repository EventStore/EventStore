// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

namespace EventStore.Core.XUnit.Tests.RateLimiting;

//qq probably move the one out of ITransactionFileTracker
public enum Source {
	Unknown,
	Archive,
	// Archive cache?
	ChunkCache,
	FileSystem,
	Count = FileSystem,
}

// could be the chunk, or perhaps the chunkrequest, it just needs a way to be mapped to
// the source that we want to read and with what priority
public record struct Resource(Source Source, Priority Priority) : IPriorityResource { };

// PartitionKey is how we partition up the resources space, each partition has a limiter
// in a way this is essentially the limiter id.
public record struct PartitionKey(Source Source) { };

public enum Priority {
	High,
	Medium,
	Low,
	Count = Low,
}

public interface IPriorityResource {
	Priority Priority { get; }
	Source Source { get; }
}

