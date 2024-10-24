// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using DotNext;

namespace EventStore.Core.LogAbstraction;

/// Looks up a name given a value
public interface INameLookup<TValue> {
	/// returns false if there is no max (i.e. the source is empty)
	ValueTask<Optional<TValue>> TryGetLastValue(CancellationToken token);

	ValueTask<string> LookupName(TValue value, CancellationToken token);
}
