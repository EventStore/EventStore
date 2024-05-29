using System;
using System.Collections.Generic;
using EventStore.Cluster;

namespace EventStore.Core.Services.Storage.Scavenge;

public record AutoScavengeClusterNodesChanged(
	DateTime Date, List<EndPoint> Nodes);
