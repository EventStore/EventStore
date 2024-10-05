// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.LogAbstraction;

namespace EventStore.Core.XUnit.Tests.LogAbstraction;

public class MockExistenceFilterInitializer : INameExistenceFilterInitializer {
	private readonly string[] _names;

	public MockExistenceFilterInitializer(params string[] names) {
		_names = names;
	}

	public void Initialize(INameExistenceFilter filter, long truncateToPosition) {
		int checkpoint = 0;
		foreach (var name in _names) {
			filter.Add(name);
			filter.CurrentCheckpoint = checkpoint++;
		}
	}
}
