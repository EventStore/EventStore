// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.POC.ConnectorsEngine.Processing.Checkpointing;

namespace Eventstore.POC.Tests.Processing;
public class ResetPointTests {
	[Fact]
	public void greaterthan_lessthan() {
		ResetPoint[] resetPoints = [
			new(0, new(0, 0)),
			new(0, new(0, 1)),
			new(0, new(1, 0)),
			new(0, new(1, 1)),
			new(1, new(0, 0)),
			new(1, new(0, 1)),
			new(1, new(1, 0)),
			new(1, new(1, 1)),
			new(1, new(1, 2)),
			new(1, new(2, 1)),
			new(2, new(1, 1)),
		];

		for (var i = 0; i < resetPoints.Length - 1; i++) {
			var a = resetPoints[i];
			var b = resetPoints[i + 1];
			Assert.True(a < b);
			Assert.True(b > a);
		}
	}
}
