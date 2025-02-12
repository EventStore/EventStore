// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.AutoScavenge.TimeProviders;

namespace EventStore.AutoScavenge.Tests.TimeProviders;

public class FakeTimeProvider : ITimeProvider {
	private DateTime _inner = DateTime.Now.Date;

	public DateTime Now => _inner;

	public void FastForward(TimeSpan timeSpan) {
		_inner = _inner.Add(timeSpan);
	}

	public DateTime NowThenFastForward(TimeSpan timeSpan) {
		var now = _inner;
		_inner = _inner.Add(timeSpan);
		return now;
	}
}
