// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.POC.ConnectorsEngine;

namespace Eventstore.POC.Tests;

public class CheckpointNamingStrategyTests {
	[Fact]
	public void Works() {
		var connectorId = "my-connector";
		var checkpointStream = "$connector-preview-my-connector-checkpoint";
		var sut = new CheckpointNamingStrategy(new ConnectorNamingStrategy());
		Assert.Equal(checkpointStream, sut.NameFor("my-connector"));
		Assert.Equal(connectorId, sut.IdFor(checkpointStream));
	}
}
