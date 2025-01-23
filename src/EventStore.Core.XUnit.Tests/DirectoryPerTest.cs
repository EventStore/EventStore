// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using Xunit;

namespace EventStore.Core.XUnit.Tests;

public class DirectoryPerTest<T> : IAsyncLifetime {
	protected DirectoryFixture<T> Fixture { get; private set; } = new();

	public virtual async Task InitializeAsync() {
		await Fixture.InitializeAsync();
	}

	public virtual async Task DisposeAsync() {
		await Fixture.DisposeAsync();
	}
}
