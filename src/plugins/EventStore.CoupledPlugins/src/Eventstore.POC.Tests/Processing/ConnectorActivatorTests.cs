// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.POC.ConnectorsEngine;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Processing;
using EventStore.POC.ConnectorsEngine.Processing.Activation;

namespace Eventstore.POC.Tests.Processing;
public class ConnectorActivatorTests {
	private readonly ConnectorActivator _sut;
	private readonly HashSet<string> _activeConnectors;

	private static Guid[] _guids = [
		Guid.Parse("00000000-4000-0000-0000-000000000001"),
		Guid.Parse("00000000-4000-0000-0000-000000000002"),
		Guid.Parse("00000000-4000-0000-0000-000000000003"),
		Guid.Parse("00000000-4000-0000-0000-000000000004"),
		Guid.Parse("00000000-4000-0000-0000-000000000005")];

	public ConnectorActivatorTests() {
		_activeConnectors = [];
		_sut = new ConnectorActivator(
			activationPolicy: new DistributingActivationPolicy(),
			connectorFactory: state => new AdHocConnector(
				() => _activeConnectors.Add(state.Id),
				() => _activeConnectors.Remove(state.Id)));
	}

	private static ContextMessage<T> MessageFor<T>(string connector, T payload, ulong eventNumber = 0) =>
		new(Guid.NewGuid(), connector, eventNumber, payload);

	private void CreateConnector(string name, NodeState affinity, bool enabled) {
		_sut.Handle(MessageFor(name, new Events.ConnectorCreated("filter", "sink", affinity, 5)));
		if (enabled)
			_sut.Handle(MessageFor(name, new Events.ConnectorEnabled()));
	}

	private void CreateConnectors() {
		CreateConnector("leader1", NodeState.Leader, enabled: true);
		CreateConnector("leader2", NodeState.Leader, enabled: true);
		CreateConnector("follower1", NodeState.Follower, enabled: true);
		CreateConnector("follower2", NodeState.Follower, enabled: true);
		CreateConnector("follower3", NodeState.Follower, enabled: true);
		CreateConnector("ror1", NodeState.ReadOnlyReplica, enabled: true);
		CreateConnector("ror2", NodeState.ReadOnlyReplica, enabled: true);
		CreateConnector("ror3", NodeState.ReadOnlyReplica, enabled: true);
	}

	private void HandleGossip(int thisNode, NodeState[] upNodes, NodeState[]? downNodes = null) {
		var i = 0;
		var members = new List<Member>();

		foreach (var x in upNodes)
			members.Add(new Member(_guids[i++], x, true));

		foreach (var x in downNodes ?? [])
			members.Add(new Member(_guids[i++], x, false));

		_sut.Handle(MessageFor("$gossip", new Messages.GossipUpdated(
			members[thisNode].InstanceId,
			[.. members])));
	}

	[Fact]
	public void active_after_caught_up_then_gossip() {
		// given
		CreateConnector("leader1", NodeState.Leader, enabled: true);
		_sut.Handle(new Messages.CaughtUp());
		Assert.Equal([], _activeConnectors);

		// when
		HandleGossip(0, [NodeState.Leader, NodeState.Follower, NodeState.Follower]);

		// then
		Assert.Equal(["leader1"], _activeConnectors);
	}

	[Fact]
	public void active_after_gossip_then_caught_up() {
		// given
		CreateConnector("leader1", NodeState.Leader, enabled: true);
		HandleGossip(0, [NodeState.Leader, NodeState.Follower, NodeState.Follower]);
		Assert.Equal([], _activeConnectors);

		// when
		_sut.Handle(new Messages.CaughtUp());

		// then
		Assert.Equal(["leader1"], _activeConnectors);
	}

	[Fact]
	public void when_all_nodes_up_then_activates_correct_connectors_on_leader() {
		// given
		CreateConnectors();
		_sut.Handle(new Messages.CaughtUp());

		// when
		HandleGossip(0, [NodeState.Leader, NodeState.Follower, NodeState.Follower, NodeState.ReadOnlyReplica]);

		// then
		Assert.Equal(["leader1", "leader2"], _activeConnectors);
	}

	[Fact]
	public void when_all_nodes_up_then_activates_correct_connectors_on_follower1() {
		// given
		CreateConnectors();
		_sut.Handle(new Messages.CaughtUp());

		// when
		HandleGossip(1, [NodeState.Leader, NodeState.Follower, NodeState.Follower, NodeState.ReadOnlyReplica]);

		// then
		Assert.Equal(["follower1"], _activeConnectors);
	}

	[Fact]
	public void when_all_nodes_up_then_activates_correct_connectors_on_follower2() {
		// given
		CreateConnectors();
		_sut.Handle(new Messages.CaughtUp());

		// when
		HandleGossip(2, [NodeState.Leader, NodeState.Follower, NodeState.Follower, NodeState.ReadOnlyReplica]);

		// then
		Assert.Equal(["follower2", "follower3"], _activeConnectors);
	}

	[Fact]
	public void when_all_nodes_up_then_activates_correct_connectors_on_ror() {
		// given
		CreateConnectors();
		_sut.Handle(new Messages.CaughtUp());

		// when
		HandleGossip(3, [NodeState.Leader, NodeState.Follower, NodeState.Follower, NodeState.ReadOnlyReplica]);

		// then
		Assert.Equal(["ror1", "ror2", "ror3"], _activeConnectors);
	}

	[Fact]
	public void when_no_leader_then_leader_connectors_are_not_activated() {
		NodeState[] members = [NodeState.Unmapped, NodeState.Follower, NodeState.Follower, NodeState.ReadOnlyReplica];

		// given
		CreateConnectors();
		_sut.Handle(new Messages.CaughtUp());

		// when first follower
		HandleGossip(1, members);

		// then no leader connectors
		Assert.Equal(["follower1"], _activeConnectors);

		// when second follower
		HandleGossip(2, members);

		// then no leader connectors
		Assert.Equal(["follower2", "follower3"], _activeConnectors);

		// when ror
		HandleGossip(3, members);

		// then no leader connectors
		Assert.Equal(["ror1", "ror2", "ror3"], _activeConnectors);
	}

	[Fact]
	public void when_no_followers_then_follower_connectors_are_not_activated() {
		NodeState[] members = [NodeState.Leader, NodeState.Unmapped, NodeState.Unmapped, NodeState.ReadOnlyReplica];

		// given
		CreateConnectors();
		_sut.Handle(new Messages.CaughtUp());

		// when leader
		HandleGossip(0, members);

		// then no follower connectors
		Assert.Equal(["leader1", "leader2"], _activeConnectors);

		// when ror
		HandleGossip(3, members);

		// then no follower connectors
		Assert.Equal(["ror1", "ror2", "ror3"], _activeConnectors);
	}

	[Fact]
	public void when_one_follower_then_follower_activates_all_follower_connectors() {
		NodeState[] members = [NodeState.Leader, NodeState.Follower, NodeState.Unmapped, NodeState.ReadOnlyReplica];

		// given
		CreateConnectors();
		_sut.Handle(new Messages.CaughtUp());

		// when follower
		HandleGossip(1, members);

		// then all follower connectors
		Assert.Equal(["follower1", "follower2", "follower3"], _activeConnectors);
	}

	[Fact]
	public void when_no_ror_then_followers_activate_ror_connectors() {
		NodeState[] members = [NodeState.Leader, NodeState.Follower, NodeState.Follower];

		// given
		CreateConnectors();
		_sut.Handle(new Messages.CaughtUp());

		// when first follower
		HandleGossip(1, members);

		// then
		Assert.Equal(["follower1", "ror2", "ror3"], _activeConnectors);

		// when second follower
		HandleGossip(2, members);

		// then
		Assert.Equal(["follower2", "follower3", "ror1"], _activeConnectors);
	}

	[Fact]
	public void down_nodes_are_ignored() {
		// given
		CreateConnectors();
		_sut.Handle(new Messages.CaughtUp());

		// when
		HandleGossip(
			1,
			upNodes: [NodeState.Leader, NodeState.Follower],
			downNodes: [NodeState.Follower, NodeState.ReadOnlyReplica]);

		// then
		Assert.Equal(
			["follower1", "follower2", "follower3", "ror1", "ror2", "ror3"],
			_activeConnectors);
	}

	[Fact]
	public void unmapped_nodes_are_ignored() {
		// given
		CreateConnectors();
		_sut.Handle(new Messages.CaughtUp());

		// when
		HandleGossip(
			1,
			upNodes: [NodeState.Leader, NodeState.Follower, NodeState.Unmapped, NodeState.Unmapped]);

		// then
		Assert.Equal(
			["follower1", "follower2", "follower3", "ror1", "ror2", "ror3"],
			_activeConnectors);
	}

	[Fact]
	public void respect_enabled_and_disabled() {
		// given
		_sut.Handle(new Messages.CaughtUp());
		HandleGossip(0, [NodeState.Leader, NodeState.Follower, NodeState.Follower, NodeState.ReadOnlyReplica]);

		// when created then not active
		CreateConnector("leader1", NodeState.Leader, enabled: false);
		Assert.Equal([], _activeConnectors);

		// when enabled then active
		_sut.Handle(MessageFor("leader1", new Events.ConnectorEnabled()));
		Assert.Equal(["leader1"], _activeConnectors);

		// when disabled then not active
		_sut.Handle(MessageFor("leader1", new Events.ConnectorDisabled()));
		Assert.Equal([], _activeConnectors);
	}
}
