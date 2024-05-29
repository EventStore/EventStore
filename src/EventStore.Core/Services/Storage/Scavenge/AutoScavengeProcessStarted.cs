using System;
using System.Collections.Generic;
using EventStore.Cluster;

namespace EventStore.Core.Services.Storage.Scavenge;

public record AutoScavengeProcessStarted(
	DateTime Started,
	List<EndPoint> Nodes);
