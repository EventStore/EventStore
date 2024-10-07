// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.LogAbstraction.Common;

public class NameExistenceFilterValueLookupDecorator<TValue> : IValueLookup<TValue> {
	private readonly IValueLookup<TValue> _wrapped;
	private readonly INameExistenceFilter _existenceFilter;

	public NameExistenceFilterValueLookupDecorator(
		IValueLookup<TValue> wrapped,
		INameExistenceFilter existenceFilter) {

		_wrapped = wrapped;
		_existenceFilter = existenceFilter;
	}

	public TValue LookupValue(string name) {
		if (_existenceFilter.MightContain(name))
			return _wrapped.LookupValue(name);

		return default;
	}
}
