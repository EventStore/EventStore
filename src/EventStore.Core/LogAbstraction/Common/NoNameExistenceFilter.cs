// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.LogAbstraction.Common;

public class NoNameExistenceFilter : INameExistenceFilter {
	public void Initialize(INameExistenceFilterInitializer source, long truncateToPosition) { }
	public void TruncateTo(long checkpoint) { }
	public void Verify(double corruptionThreshold) { }
	public long CurrentCheckpoint { get; set; } = -1;

	public void Add(string name) { }
	public void Add(ulong hash) { }
	public bool MightContain(string name) => true;
	public void Dispose() { }
}
