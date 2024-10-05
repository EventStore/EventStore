// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests;

[TestFixture]
public class ExclusiveDbLockTests {
	[Test]
	public async Task can_release_when_running_in_task_pool() {
		using var sut = new ExclusiveDbLock(GetDbPath());
		Assert.True(sut.Acquire());
		Assert.True(sut.IsAcquired);
		await Task.Delay(1);
		sut.Release();
	}

	[Test]
	public void acquiring_twice_throws() {
		using var sut = new ExclusiveDbLock(GetDbPath());
		sut.Acquire();
		Assert.Throws<InvalidOperationException>(() => sut.Acquire());
	}

	[Test]
	public void releasing_before_acquiring_throws() {
		using var sut = new ExclusiveDbLock(GetDbPath());
		Assert.Throws<InvalidOperationException>(() => sut.Release());
	}

	private static string GetDbPath() => $"/tmp/eventstore/{Guid.NewGuid()}";


}
