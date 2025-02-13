// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.XUnit.Tests.RateLimiting;

public record FakeRequest(int Number, Source Source, RateLimiting.Priority Priority) {
	public async ValueTask Process(CancellationToken token) {
		Console.WriteLine($"Processing {Number}...");
		await Task.Delay(100, token);
		Console.WriteLine($"DONE Processing {Number}.");
	}

	public void Reject(string message) {
		Console.WriteLine($"REJECTED {Number}. {message}");
	}
}
