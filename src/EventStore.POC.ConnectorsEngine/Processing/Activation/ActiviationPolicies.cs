// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;

namespace EventStore.POC.ConnectorsEngine.Processing.Activation;

// the enabled connectors want to run on the nodes they have affinity to.
// if there are _multiple_ such nodes the connectors are shared among them
// if there are _no_ such nodes then:
//    if connector wants to run on leader but there isn't one, dont run.
//    if connector wants to run on follower but there isn't one, don't run.
//    if connector wants to run on a ror but there isn't one, run on a follower.
public class DistributingActivationPolicy : IActivationPolicy {
	public IReadOnlySet<string> CalculateActive(
		Guid nodeId,
		NodeState nodeState,
		IDictionary<Guid, NodeState> aliveMembers,
		IDictionary<string, ConnectorState> connectors) {

		// nodes in unmapped states run no connectors
		if (nodeState == NodeState.Unmapped)
			return StartNone();

		// leader nodes run connectors that have leader affinity
		if (nodeState == NodeState.Leader)
			return StartWithAffinity(NodeState.Leader, connectors);

		// node is follower or ror
		// determine how many other nodes have the same state and which index we are among them
		// this is how many we need to share between and which slice we will take
		if (!CalcNodesInSameState(
			nodeId: nodeId,
			nodeState: nodeState,
			members: aliveMembers,
			nodeIndex: out var slice,
			nodeCount: out var numSlices)) {
			// we aren't listed in the cluster nodes, don't run anything.
			return StartNone();
		}

		// find the enabled connectors, partition by affinity, and sort so that we can slice them up.
		SortConnectors(
			connectors,
			out var enabledFollowerConnectors,
			out var enabledReadOnlyReplicaConnectors);

		// follower nodes run their slice of follower connectors
		// plus their slice of ror connectors if there are no rors
		if (nodeState == NodeState.Follower) {
			return CalculateActiveOnFollower(
				slice: slice,
				numSlices: numSlices,
				members: aliveMembers,
				followerConnectors: enabledFollowerConnectors,
				rorConnectors: enabledReadOnlyReplicaConnectors);

		} 
		
		// readonlyreplica nodes run their slice of connectors that have readonlyreplica affinity
		if (nodeState == NodeState.ReadOnlyReplica) {
			return StartSlice(
				slice: slice,
				numSlices: numSlices,
				connectors: enabledReadOnlyReplicaConnectors);
		}

		Serilog.Log.Warning("Unhandled node state {nodeState}", nodeState);
		return StartNone();
	}

	private static void SortConnectors(
		IDictionary<string, ConnectorState> connectors,
		out SortedSet<string> followerConnectors,
		out SortedSet<string> rorConnectors) {

		followerConnectors = new SortedSet<string>();
		rorConnectors = new SortedSet<string>();

		foreach (var kvp in connectors) {
			if (!kvp.Value.Enabled)
				continue;

			switch (kvp.Value.Affinity) {
				case NodeState.Follower:
					followerConnectors.Add(kvp.Key);
					break;
				case NodeState.ReadOnlyReplica:
					rorConnectors.Add(kvp.Key);
					break;
				default:
					break;
			}
		}
	}

	private static IReadOnlySet<string> StartWithAffinity(
		NodeState affinity,
		IDictionary<string, ConnectorState> connectors) {

		var active = new HashSet<string>();

		foreach (var kvp in connectors) {
			if (kvp.Value.Enabled && kvp.Value.Affinity == affinity)
				active.Add(kvp.Key);
		}

		return active;
	}

	private static IReadOnlySet<string> StartNone() {
		return new HashSet<string>();
	}

	private static IReadOnlySet<string> CalculateActiveOnFollower(
		int slice,
		int numSlices,
		IDictionary<Guid, NodeState> members,
		SortedSet<string> followerConnectors,
		SortedSet<string> rorConnectors) {

		var active = StartSlice(slice, numSlices, followerConnectors);

		var foundRor = false;
		foreach (var nodeState in members.Values) {
			if (nodeState == NodeState.ReadOnlyReplica) {
				foundRor = true;
			}
		}

		if (!foundRor) {
			// last slice is biggest if any is, so
			// if we took the smaller follower slice take the bigger ror slice
			foreach (var c in StartSlice(numSlices - 1 - slice, numSlices, rorConnectors)) {
				active.Add(c);
			}
		}

		return active;
	}

	// calculates the count of nodes in the same state as us
	// and which index we are among them.
	// nodeIndex is -1 if we are not among them at all.
	private static bool CalcNodesInSameState(
		Guid nodeId,
		NodeState nodeState,
		IDictionary<Guid, NodeState> members,
		out int nodeIndex,
		out int nodeCount) {

		var membersInSameState = new SortedList<Guid, bool>();
		nodeIndex = -1;
		var i = 0;
		foreach (var member in members) {
			if (member.Value == nodeState) {
				membersInSameState.Add(member.Key, true);
				if (member.Key == nodeId) {
					nodeIndex = i;
				}
				i++;
			}
		}
		nodeCount = membersInSameState.Count;
		return nodeIndex != -1;
	}

	private static HashSet<string> StartSlice(
		int slice,
		int numSlices,
		SortedSet<string> connectors) {

		var active = new HashSet<string>();

		var slicer = new Slicer(
			numItems: connectors.Count,
			numSlices: numSlices,
			slice: slice);

		var connectorIndex = 0;
		foreach (var connectorId in connectors) {
			if (slicer.IsInSlice(itemIndex: connectorIndex)) {
				active.Add(connectorId);
			}
			connectorIndex++;
		}

		return active;
	}

	private readonly struct Slicer {
		public readonly int _lowItemIndex; // incl
		public readonly int _highItemIndex; // excl

		public Slicer(
			int numItems,
			int numSlices,
			int slice) {

			var sizeOfSlice = numItems / numSlices;
			_lowItemIndex = sizeOfSlice * slice;
			_highItemIndex = _lowItemIndex + sizeOfSlice;

			if (slice == numSlices - 1) {
				_highItemIndex = numItems;
			}
		}

		public bool IsInSlice(int itemIndex) =>
			_lowItemIndex <= itemIndex && itemIndex < _highItemIndex;
	}
}
