// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.LogAbstraction.Common;

public class NoNameExistenceFilter : INameExistenceFilter {
	public ValueTask Initialize(INameExistenceFilterInitializer source, long truncateToPosition,
		CancellationToken token)
		=> token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;

	public void TruncateTo(long checkpoint) { }
	public void Verify(double corruptionThreshold) { }
	public long CurrentCheckpoint { get; set; } = -1;

	public void Add(string name) { }
	public void Add(ulong hash) { }
	public bool MightContain(string name) => true;
	public void Dispose() { }
}
