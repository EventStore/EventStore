// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.POC.ConnectorsEngine.Processing.Activation;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Processing.Checkpointing;
using Serilog;

namespace EventStore.POC.ConnectorsEngine.Processing;

// Read side. Single Threaded.
// This handles connector events and node state events and activates/deactivates instances of
// connectors according to the provided policy.
public class ConnectorActivator : StateMachine {
	private static readonly ILogger _logger = Log.ForContext<ConnectorActivator>();

	private readonly Dictionary<string, ConnectorState> _connectorStates = new();
	private readonly Dictionary<string, IConnector> _activeConnectors = new();

	private Guid _nodeId;
	private NodeState _nodeState;
	private Dictionary<Guid, NodeState> _members = new();

	private bool _catchingUp;

	private readonly IActivationPolicy _activationPolicy;
	private readonly Func<ConnectorState, IConnector> _connectorFactory;

	public ConnectorActivator(
		IActivationPolicy activationPolicy,
		Func<ConnectorState, IConnector> connectorFactory) {

		_catchingUp = true;
		_activationPolicy = activationPolicy;
		_connectorFactory = connectorFactory;

		Register<Messages.CaughtUp>(Handle);
		Register<ContextMessage<Events.ConnectorCreated>>(Handle);
		Register<ContextMessage<Events.ConnectorEnabled>>(Handle);
		Register<ContextMessage<Events.ConnectorDisabled>>(Handle);
		Register<ContextMessage<Events.ConnectorReset>>(Handle);
		Register<ContextMessage<Events.ConnectorDeleted>>(Handle);
		Register<Messages.GetConnectorList>(Handle);
		Register<Messages.GetActiveConnectorsList>(Handle);
		Register<ContextMessage<Messages.GossipUpdated>>(Handle);
	}

	private void Handle(Messages.CaughtUp msg) {
		_catchingUp = false;
		UpdateAllActivations();
	}

	private void Handle(ContextMessage<Messages.GossipUpdated> m) {
		var msg = m.Payload;
		var newMembers = new Dictionary<Guid, NodeState>();
		foreach (var member in msg.Members) {
			//qq only include members that are adequately caught up
			if (member.IsAlive) {
				newMembers[member.InstanceId] = member.State;
			}
		}

		_nodeState = newMembers.TryGetValue(msg.NodeId, out var nodeState)
			? nodeState
			: NodeState.Unmapped;

		var clusterChanged = false;

		// nodeId can change if we are hosted externally and our node restarts
		if (msg.NodeId != _nodeId) {
			_nodeId = msg.NodeId;
			clusterChanged = true;
		}

		// change in number of members => cluster changed
		if (_members.Count != newMembers.Count)
			clusterChanged = true;

		// any member has a different state to before => cluster changed
		foreach (var newMember in newMembers) {
			if (!_members.TryGetValue(newMember.Key, out var oldState) ||
				newMember.Value != oldState) {
				clusterChanged = true;
			}
		}

		if (clusterChanged) {
			_members = newMembers;
			UpdateAllActivations();
		}
	}

	private void Handle(ContextMessage<Events.ConnectorCreated> m) {
		var msg = m.Payload;
		var state = new ConnectorState(
			Id: m.EntityId,
			Filter: msg.Filter,
			Sink: msg.Sink,
			Affinity: msg.Affinity,
			Enabled: false,
			CheckpointConfig: new(msg.CheckpointInterval));
		_connectorStates[m.EntityId] = state;
		UpdateAllActivations();
	}

	private void Handle(ContextMessage<Events.ConnectorEnabled> msg) {
		ReadModifyWriteState(msg.EntityId, msg, static (state, _) => state with { Enabled = true });
	}

	private void Handle(ContextMessage<Events.ConnectorDisabled> msg) {
		ReadModifyWriteState(msg.EntityId, msg, static (state, _) => state with { Enabled = false });
	}

	private void Handle(ContextMessage<Events.ConnectorReset> msg) {
		ReadModifyWriteState(msg.EntityId, msg, static (state, msg_) => state with {
			ResetTo = new ResetPoint(
				msg_.EventNumber,
				msg_.Payload.CommitPosition is not null || msg_.Payload.PreparePosition is not null
					? new(msg_.Payload.CommitPosition ?? 0, msg_.Payload.PreparePosition ?? 0)
					: null)
		});
	}

	private void Handle(ContextMessage<Events.ConnectorDeleted> msg) {
		Deactivate(msg.EntityId);
		_connectorStates.Remove(msg.EntityId);
		UpdateAllActivations();
	}

	private void Handle(Messages.GetConnectorList msg) {
		//qq we could calculate where each node is supposed to be running here and add it in
		msg.Reply(_connectorStates.Values.ToArray());
	}

	private void Handle(Messages.GetActiveConnectorsList msg) {
		msg.Reply(_activeConnectors.Keys.ToArray());
	}

	private void ReadModifyWriteState<T>(string connectorId, T msg, Func<ConnectorState, T, ConnectorState> modify) {
		if (!_connectorStates.TryGetValue(connectorId, out var state))
			return;

		Deactivate(connectorId);
		_connectorStates[connectorId] = modify(state, msg);
		UpdateAllActivations();
	}

	private void UpdateAllActivations() {
		if (_catchingUp)
			return;

		var desiredActiveConnectors = _activationPolicy.CalculateActive(
			_nodeId, _nodeState, _members, _connectorStates);

		foreach (var kvp in _connectorStates) {
			var connectorId = kvp.Key;
			var state = kvp.Value;

			var shouldBeActive = desiredActiveConnectors.Contains(connectorId);
			var isActive = _activeConnectors.TryGetValue(connectorId, out _);

			if (!isActive && shouldBeActive) {
				Activate(state);
			} else if (!shouldBeActive && isActive) {
				Deactivate(connectorId);
			}
		}
	}

	private void Activate(ConnectorState state) {
		_logger.Information("Connectors: ACTIVATING {connectorId} on {nodeState}",
			state.Id, _nodeState);

		try {
			var connector = _connectorFactory(state);
			_activeConnectors[state.Id] = connector;
			_ = connector.RunAsync();
		} catch (Exception ex) {
			_logger.Error(ex, "Connectors: Failed to activate connector {connectorId} on {nodeState}",
				state.Id, _nodeState);
		}
	}

	private void Deactivate(string id) {
		if (!_activeConnectors.Remove(id, out var connector)) {
			return;
		}

		_logger.Information("Connectors: DEACTIVATING {connectorId} on {nodeState}",
			id, _nodeState);
		connector.Dispose();
	}
}
