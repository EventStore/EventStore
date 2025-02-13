// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using Xunit;

namespace EventStore.Core.XUnit.Tests.RateLimiting;

public class AlternateTests {
	[Fact]
	public void Test() {
		var processor = new ConcurrentConsumerBySource<FakeRequest>(
			//qq concurrencyLimit wants to be different per source and dynamically changeable
			concurrencyLimit: 100,
			r => r.Source,
			r => r.Priority,
			// this is called in (priority, fifo) order, with the right degree of concurrency.
			(r, ct) => r.Process(ct));

		// main thread pumping the request queue (StorageReaderService)
		foreach (var request in RateLimitingTests.GenerateRequests()) {
			if (!processor.TryEnqueue(request)) {
				request.Reject("queue full");
			}
		}
	}

	// so far this is pretty good for when we have a queue of requests where each one
	// has a particular priority and source.
	// however the actual requests that we are dealing with are more complex than tha. they:
	//  - potentially require access to multiple sources
	//  - potentially require access to multiple sources _to determine which other sources they need_
}
