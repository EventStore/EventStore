// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net;

namespace EventStore.Common.Utils;

public class IpWithClusterDnsEndPoint : IPEndPoint {
	public string ClusterDnsName { get; }

	public IpWithClusterDnsEndPoint(IPAddress address, string clusterDns, int port) : base(address, port) {
		Ensure.NotNull(clusterDns, "clusterDns");
		Ensure.NotNull(address, "address");
		ClusterDnsName = clusterDns;
	}

	public override string ToString() => $"{base.ToString()}/{ClusterDnsName}";
}
