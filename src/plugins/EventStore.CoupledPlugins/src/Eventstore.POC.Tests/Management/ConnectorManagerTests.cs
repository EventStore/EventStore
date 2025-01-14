// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.POC.ConnectorsEngine;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Management;

namespace Eventstore.POC.Tests.Management;

public class ConnectorManagerTests {
	private static readonly string _connectorA = "connectorA";
	private static readonly string _myFilter = "myFilter";
	private static readonly string _consoleSink = "console://";
	private static readonly NodeState _leaderAffinity = NodeState.Leader;

	private static void Run(Func<TestDefinition<ConnectorManager>, TestDefinition<ConnectorManager>> configure) {
		new Runner<ConnectorManager>(new ConnectorManager()).Run(configure(new()));
	}

	class Histories {
		public static readonly Message[] EnabledConnector = [
			new Events.ConnectorCreated(_myFilter, _consoleSink, _leaderAffinity, 5),
			new Events.ConnectorReset(CommitPosition: null, PreparePosition: null),
			new Events.ConnectorEnabled()];

		public static readonly Message[] DisabledConnector = [
			.. EnabledConnector,
			new Events.ConnectorDisabled()];
	}

	[Fact]
	public void given_nothing_can_create() => Run(def => def
		.Given()
		.When(sut => sut.Create(
			connectorId: _connectorA,
			filter: _myFilter,
			sink: _consoleSink,
			affinity: _leaderAffinity,
			enable: true,
			checkpointInterval: 5))
		.Then(
			new Events.ConnectorCreated(_myFilter, _consoleSink, _leaderAffinity, 5),
			new Events.ConnectorReset(CommitPosition: null, PreparePosition: null),
			new Events.ConnectorEnabled())
		// the `Giving` method allows us to ensure that we ended up with the expected overall history
		// such that we can use it with confidence as the given of another test.
		.Giving(Histories.EnabledConnector));

	[Fact]
	public void given_enabled_can_disable() => Run(def => def
		.Given(Histories.EnabledConnector)
		.When(sut => sut.Disable())
		.Then(new Events.ConnectorDisabled())
		.Giving(Histories.DisabledConnector));

	[Fact]
	public void given_enabled_can_enable() => Run(def => def
		.Given(Histories.EnabledConnector)
		.When(sut => sut.Enable())
		.Then());

	[Fact]
	public void given_disabled_can_enable() => Run(def => def
		.Given(Histories.DisabledConnector)
		.When(sut => sut.Enable())
		.Then(new Events.ConnectorEnabled()));
}
