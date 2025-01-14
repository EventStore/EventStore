// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.POC.IO.Core;

namespace EventStore.AutoScavenge.Tests;

public class FakeOperationsClient : IOperationsClient {
	public bool ResignationIssued { get; private set; }
	public void Resign() {
		ResignationIssued = true;
	}
}
