// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Other;

[TestFixture]
class Stopwatch {
	[Test]
	public void MeasureStopwatch() {
		var sw = new System.Diagnostics.Stopwatch();
		var measured = new System.Diagnostics.Stopwatch();
		sw.Reset();
		sw.Start();
		measured.Start();
		measured.Stop();
		TestHelper.Consume(measured.ElapsedMilliseconds);
		sw.Stop();
		TestHelper.Consume(sw.ElapsedMilliseconds);
		measured.Reset();
		sw.Reset();

		sw.Start();
		sw.Stop();
		var originalTime = sw.ElapsedMilliseconds;
		sw.Reset();

		sw.Start();
		for (var i = 0; i < 1000000; i++) {
			measured.Start();
			measured.Stop();
			TestHelper.Consume(measured.ElapsedMilliseconds);
		}

		sw.Stop();
		var measuredTime = sw.ElapsedMilliseconds;
		Console.WriteLine(measuredTime - originalTime);
	}
}
