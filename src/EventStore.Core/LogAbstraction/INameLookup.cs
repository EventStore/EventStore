// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.LogAbstraction;

/// Looks up a name given a value
public interface INameLookup<TValue> {
	/// returns false if there is no max (i.e. the source is empty)
	bool TryGetLastValue(out TValue last);

	bool TryGetName(TValue value, out string name);

	string LookupName(TValue value) {
		TryGetName(value, out var name);
		return name;
	}
}
